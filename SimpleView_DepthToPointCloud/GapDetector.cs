using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleView_DepthToPointCloud
{
    /// <summary>
    /// 卷线缝隙检测 + MAD (Modified Z-score) 计算
    /// + 基于先验知识的峰值检测（骑线/跨线/压线/缺线）
    ///
    /// 检测原理：
    /// 线激光传感器的深度轮廓是一条连续的线。
    /// 卷线的线材在轮廓上表现为起伏的波峰和波谷，
    /// 波峰 = 线材（较高的位置，靠近传感器，depth值更小）
    /// 波谷 = 缝隙（线材之间的凹槽，depth值更大）
    ///
    /// 原有逻辑：以最上层（最高波峰）为基准，往下找深度波谷作为缝隙位置，MAD统计异常。
    /// 新增逻辑：利用已知线宽d、线高h、每层匝数N，对波峰做物理约束校验，分类输出异常类型。
    /// </summary>
    public class GapDetector
    {
        // =====================================================================
        // 检测结果
        // =====================================================================
        public class GapResult
        {
            // ── 原有字段（不动）──
            public double MadValue { get; set; }
            public double ModifiedZScore { get; set; }
            public bool IsAnomaly { get; set; }
            public double AvgGapMm { get; set; }
            public int GapCount { get; set; }
            public List<double> GapDistancesMm { get; set; }
            public bool HasValidData { get; set; }

            // ── 新增字段 ──
            /// <summary>从峰数量推断的当前匝数</summary>
            public int InferredTurn { get; set; }
            /// <summary>从峰高度推断的当前层数（需要标定基准后才准确，-1表示未标定）</summary>
            public int InferredLayer { get; set; } = -1;
            /// <summary>分类异常列表</summary>
            public List<DefectInfo> Defects { get; set; } = new List<DefectInfo>();
        }

        // =====================================================================
        // 异常类型
        // =====================================================================
        public enum DefectType
        {
            RidingWire,   // 骑线：某峰高度明显偏高
            SkipWire,     // 跨线：相邻峰间距 > 1.5d
            OverlapWire,  // 压线：相邻峰间距 < 0.7d
            MissingWire   // 缺线：峰总数不足
        }

        public class DefectInfo
        {
            public DefectType Type { get; set; }
            public string Message { get; set; }
            /// <summary>异常发生位置（mm，相对轮廓左端）</summary>
            public double PositionMm { get; set; }
        }

        // =====================================================================
        // 配置参数
        // =====================================================================

        // ── 原有参数（不动）──
        /// <summary>MAD Modified Z-score 异常阈值，原有逻辑使用</summary>
        public double ThresholdZ { get; set; } = 5.0;
        /// <summary>每像素对应毫米数，由SDK的LSLProfileCoordXUnit填入</summary>
        public double MmPerPixel { get; set; } = 0.02;

                // ── 新增：基准线存储 ──
        private float[] m_baseline = null;
        public bool HasBaseline => m_baseline != null;

        // ── 新增先验知识参数（由UI的NumericUpDown同步过来）──
        /// <summary>线宽 d (mm)</summary>
        public float WireWidth { get; set; } = 4.10f;
        /// <summary>线高 h (mm)</summary>
        public float WireHeight { get; set; } = 1.70f;
        /// <summary>每层匝数 N</summary>
        public int TurnsPerLayer { get; set; } = 8;
        /// <summary>总层数 L</summary>
        public int TotalLayers { get; set; } = 9;

        // ── 异常判断系数（可在UI中扩展调整，当前用默认值）──
        /// <summary>峰高度偏差超过此比例×WireHeight则判骑线，默认0.30</summary>
        public float HeightTolerance { get; set; } = 0.30f;
        /// <summary>间距小于此比例×WireWidth则判压线，默认0.70</summary>
        public float SpacingMinRatio { get; set; } = 0.70f;
        /// <summary>间距大于此比例×WireWidth则判跨线，默认1.50</summary>
        public float SpacingMaxRatio { get; set; } = 1.50f;
        /// <summary>峰数量低于此比例×TurnsPerLayer则判缺线，默认0.70</summary>
        public float MissingRatio { get; set; } = 0.70f;

        // =====================================================================
        // 基准线校准
        // =====================================================================
        /// <summary>
        /// 外部校准入口：传入原始 short[] 深度数据，内部调用 Preprocess
        /// 平滑后作为基准线存储。
        /// 后续 ProcessDepthLine 会从实时数据中减去该基准线，使测量结果
        /// 以基准面为参照，消除圆筒弧面/传感器安装倾角等固定偏差。
        /// </summary>
        public void CalibrateBaseline(short[] depthData, int width)
        {
            float[] smoothed = Preprocess(depthData, width);
            if (smoothed != null)
                m_baseline = (float[])smoothed.Clone();
        }

        /// <summary>
        /// 清除已存储的基准线，恢复原始测量模式。
        /// </summary>
        public void ClearBaseline()
        {
            m_baseline = null;
        }

        // =====================================================================
        // 主入口：处理一行深度数据
        // =====================================================================
        /// <summary>
        /// 处理深度数据 - 通过查找轮廓波谷来检测缝隙（原有逻辑保留），
        /// 并额外执行基于先验知识的波峰校验（新增逻辑）。
        /// </summary>
        public GapResult ProcessDepthLine(short[] depthData, int width)
        {
            GapResult result = new GapResult();
            result.GapDistancesMm = new List<double>();
            result.HasValidData = false;

            if (depthData == null || width < 20)
                return result;

                        // 1. 预处理：中值滤波去噪
            float[] smoothed = Preprocess(depthData, width);
            if (smoothed == null || smoothed.Length < 20)
                return result;

            // ── 基准补偿：每点减去基准值，消除圆筒弧面误差 ──
            if (m_baseline != null && m_baseline.Length == smoothed.Length)
            {
                for (int i = 0; i < smoothed.Length; i++)
                {
                    if (smoothed[i] > 0 && m_baseline[i] > 0)
                        smoothed[i] = smoothed[i] - m_baseline[i] + 10000; // 平移到正值区间
                    // 若基准点无效则保持原值
                }
            }

            // 2. 找到最上层基准（最高波峰的深度值，depth值越小=越靠近传感器=越高）
            float topLayerDepth = float.MaxValue;
            for (int i = 0; i < smoothed.Length; i++)
            {
                if (smoothed[i] > 0 && smoothed[i] < topLayerDepth)
                    topLayerDepth = smoothed[i];
            }

            if (topLayerDepth >= float.MaxValue)
                return result;

            // 3. 计算波谷深度阈值 = 最上层 + 偏移量
            float valleyThreshold = topLayerDepth + 300; // 可调参数

            // 4. 找所有局部最小值（波谷 = 缝隙）
            List<Valley> valleys = FindValleys(smoothed, valleyThreshold);

            if (valleys.Count < 2)
            {
                // 波谷不足时仍尝试做峰值检测
                if (WireWidth > 0 && WireHeight > 0)
                    RunPeakInspection(smoothed, result);
                return result;
            }

            // 5. 计算相邻波谷间距（像素 → 毫米）
            List<double> gapDistancesMm = new List<double>();
            for (int i = 1; i < valleys.Count; i++)
            {
                double gapPixels = valleys[i].Index - valleys[i - 1].Index;
                if (gapPixels >= 10)
                    gapDistancesMm.Add(gapPixels * MmPerPixel);
            }

            if (gapDistancesMm.Count < 1)
            {
                if (WireWidth > 0 && WireHeight > 0)
                    RunPeakInspection(smoothed, result);
                return result;
            }

            result.GapDistancesMm = gapDistancesMm;
            result.GapCount = gapDistancesMm.Count;
            result.AvgGapMm = gapDistancesMm.Average();
            result.HasValidData = true;

            // 6. MAD + Modified Z-score（原有逻辑，完全保留）
            double median = Median(gapDistancesMm);
            double mad = MedianAbsoluteDeviation(gapDistancesMm, median);
            result.MadValue = mad;

            double modifiedZScore = 0;
            if (mad > 0)
            {
                double maxDeviation = 0;
                foreach (double g in gapDistancesMm)
                {
                    double dev = Math.Abs(g - median);
                    if (dev > maxDeviation) maxDeviation = dev;
                }
                modifiedZScore = 0.6745 * maxDeviation / mad;
            }
            result.ModifiedZScore = modifiedZScore;
            result.IsAnomaly = (modifiedZScore > ThresholdZ);

            // 7. 新增：基于先验知识的峰值检测
            if (WireWidth > 0 && WireHeight > 0)
                RunPeakInspection(smoothed, result);

            return result;
        }

        // =====================================================================
        // 新增：基于先验知识的峰值检测
        // =====================================================================
        /// <summary>
        /// 在平滑后的深度轮廓上找波峰（线材顶部），
        /// 用已知的 WireWidth/WireHeight/TurnsPerLayer 做物理约束校验，
        /// 将异常分类为骑线/跨线/压线/缺线，写入 result.Defects。
        /// </summary>
                private void RunPeakInspection(float[] smoothed, GapResult result)
        {
            float mmPerPt = (float)MmPerPixel;
            if (mmPerPt <= 0) mmPerPt = 0.02f;

            // 峰检测参数
            // depth单位是short原始值，1mm ≈ 100 raw，所以prominence用raw单位
            float minProminenceRaw = WireHeight * 25f;   // 约0.25h对应的raw值（保守）
            float minDistMm = WireWidth * 0.7f;
            int minDistPts = Math.Max(1, (int)(minDistMm / mmPerPt));

            // 找所有波峰（depth局部最小值 = 高度局部最大值）
            List<int> peakIdx = new List<int>();
            List<float> peakVal = new List<float>(); // depth raw值

            for (int i = 3; i < smoothed.Length - 3; i++)
            {
                if (smoothed[i] <= 0) continue;

                // 局部最小值：比左右各3点都小
                bool isPeak = smoothed[i] < smoothed[i - 1]
                           && smoothed[i] < smoothed[i + 1]
                           && smoothed[i] < smoothed[i - 2]
                           && smoothed[i] < smoothed[i + 2]
                           && smoothed[i] < smoothed[i - 3]
                           && smoothed[i] < smoothed[i + 3];
                if (!isPeak) continue;

                // 计算prominence：在±(minDistPts)范围内的最大depth与当前depth的差
                int left = Math.Max(0, i - minDistPts);
                int right = Math.Min(smoothed.Length - 1, i + minDistPts);
                float localMax = smoothed[i];
                for (int k = left; k <= right; k++)
                {
                    if (smoothed[k] > localMax) localMax = smoothed[k];
                }
                float prominence = localMax - smoothed[i];
                if (prominence < minProminenceRaw) continue;

                // 最小间距去重：与上一个峰太近时保留depth更小（更高）的那个
                if (peakIdx.Count > 0 && (i - peakIdx[peakIdx.Count - 1]) < minDistPts)
                {
                    if (smoothed[i] < peakVal[peakVal.Count - 1])
                    {
                        peakIdx[peakIdx.Count - 1] = i;
                        peakVal[peakVal.Count - 1] = smoothed[i];
                    }
                    continue;
                }

                peakIdx.Add(i);
                peakVal.Add(smoothed[i]);
            }

            int peakCount = peakIdx.Count;
            result.InferredTurn = peakCount;

            if (peakCount == 0)
                return;

            // ── 层数推断（基于峰间高差）──
            // 需要两层以上且有高差才能推断层数；单层只记录匝数
            // 用"峰高度均值"相对于最高峰的差值推断：
            //   层数偏移 = round(高差 / h)，绝对层数需标定，暂输出-1
            result.InferredLayer = -1; // 绝对层数需标定基准，后续Step可扩展

            // ── 骑线检测（高度异常偏高）──
            // 计算所有峰的depth均值作为基准
            float avgDepth = peakVal.Average();
            float heightThreshRaw = WireHeight * HeightTolerance * 100f; // h×0.3 对应raw
            for (int i = 0; i < peakCount; i++)
            {
                // depth更小 = 高度更高；若某峰比均值小超过阈值 → 骑线
                float deviation = avgDepth - peakVal[i]; // 正值表示该峰比均值更高
                if (deviation > heightThreshRaw)
                {
                    double positionMm = peakIdx[i] * mmPerPt;
                    result.Defects.Add(new DefectInfo
                    {
                        Type = DefectType.RidingWire,
                        Message = string.Format("第{0}匝 X={1:F1}mm 骑线(高出约{2:F2}mm)",
                                         i + 1, positionMm, deviation / 100.0),
                        PositionMm = positionMm
                    });
                }
            }

            // ── 间距校验（跨线/压线）──
            float expectedSpacingMm = WireWidth;
            for (int i = 1; i < peakCount; i++)
            {
                float spacingPts = peakIdx[i] - peakIdx[i - 1];
                float spacingMm = spacingPts * mmPerPt;
                double posMm = peakIdx[i] * mmPerPt;

                if (spacingMm < expectedSpacingMm * SpacingMinRatio)
                {
                    result.Defects.Add(new DefectInfo
                    {
                        Type = DefectType.OverlapWire,
                        Message = string.Format("第{0}-{1}匝 间距{2:F2}mm<{3:F2}mm 压线",
                                         i, i + 1, spacingMm, expectedSpacingMm * SpacingMinRatio),
                        PositionMm = posMm
                    });
                }
                else if (spacingMm > expectedSpacingMm * SpacingMaxRatio)
                {
                    result.Defects.Add(new DefectInfo
                    {
                        Type = DefectType.SkipWire,
                        Message = string.Format("第{0}-{1}匝 间距{2:F2}mm>{3:F2}mm 跨线",
                                         i, i + 1, spacingMm, expectedSpacingMm * SpacingMaxRatio),
                        PositionMm = posMm
                    });
                }
            }

            // ── 缺线检测（峰数量不足）──
            int expectedMinTurns = (int)(TurnsPerLayer * MissingRatio);
            if (peakCount < expectedMinTurns)
            {
                result.Defects.Add(new DefectInfo
                {
                    Type = DefectType.MissingWire,
                    Message = string.Format("检测到{0}匝，期望至少{1}匝(缺线)",
                                     peakCount, expectedMinTurns),
                    PositionMm = 0
                });
            }

            // 如果发现新类型异常，标记整体异常
            if (result.Defects.Count > 0)
                result.IsAnomaly = true;
        }

        // =====================================================================
        // 原有内部方法（完全不动）
        // =====================================================================

        private class Valley
        {
            public int Index { get; set; }
            public float Depth { get; set; }
        }

        private float[] Preprocess(short[] data, int width)
        {
            float[] result = new float[width];
            int validCount = 0;

            for (int i = 0; i < width; i++)
            {
                if (data[i] > 0)
                {
                    result[i] = data[i];
                    validCount++;
                }
                else
                {
                    result[i] = -1;
                }
            }

            if (validCount < 20)
                return null;

            float[] smoothed = new float[width];
            for (int i = 0; i < width; i++)
            {
                if (result[i] < 0)
                {
                    smoothed[i] = -1;
                    continue;
                }

                List<float> window = new List<float>();
                for (int j = -1; j <= 1; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < width && result[idx] > 0)
                        window.Add(result[idx]);
                }
                if (window.Count > 0)
                {
                    window.Sort();
                    smoothed[i] = window[window.Count / 2];
                }
                else
                {
                    smoothed[i] = result[i];
                }
            }

            return smoothed;
        }

        private List<Valley> FindValleys(float[] profile, float valleyThreshold)
        {
            List<Valley> valleys = new List<Valley>();
            int n = profile.Length;

            for (int i = 2; i < n - 2; i++)
            {
                if (profile[i] < 0) continue;
                if (profile[i] > valleyThreshold) continue;

                bool isValley = true;
                for (int j = -2; j <= 2; j++)
                {
                    if (j == 0) continue;
                    int idx = i + j;
                    if (idx >= 0 && idx < n && profile[idx] > 0)
                    {
                        if (profile[i] >= profile[idx])
                        {
                            isValley = false;
                            break;
                        }
                    }
                }

                if (isValley)
                    valleys.Add(new Valley { Index = i, Depth = profile[i] });
            }

            if (valleys.Count > 1)
            {
                List<Valley> merged = new List<Valley>();
                merged.Add(valleys[0]);

                for (int i = 1; i < valleys.Count; i++)
                {
                    if (valleys[i].Index - merged[merged.Count - 1].Index <= 3)
                    {
                        if (valleys[i].Depth > merged[merged.Count - 1].Depth)
                            merged[merged.Count - 1] = valleys[i];
                    }
                    else
                    {
                        merged.Add(valleys[i]);
                    }
                }
                valleys = merged;
            }

            return valleys;
        }

        private double Median(List<double> values)
        {
            List<double> sorted = new List<double>(values);
            sorted.Sort();
            int n = sorted.Count;
            if (n % 2 == 0)
                return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
            else
                return sorted[n / 2];
        }

        private double MedianAbsoluteDeviation(List<double> values, double median)
        {
            List<double> deviations = new List<double>();
            foreach (double v in values)
                deviations.Add(Math.Abs(v - median));
            return Median(deviations);
        }

        public static short[] ExtractDepthLine(short[] depthData, int width, int height, int rowIndex)
        {
            if (rowIndex < 0) rowIndex = 0;
            if (rowIndex >= height) rowIndex = height - 1;

            short[] line = new short[width];
            int offset = rowIndex * width;
            Array.Copy(depthData, offset, line, 0, width);
            return line;
        }

        /// <summary>
        /// 取中心行上下各 halfWindow 行（共 2*halfWindow+1 行）的逐列均值，
        /// 比单行抗噪能力更强。无效值（<=0）不参与均值计算。
        /// 若某列所有行均无效，则输出 0。
        /// </summary>
        /// <param name="depthData">完整深度数据（行优先）</param>
        /// <param name="width">图像宽度（列数）</param>
        /// <param name="height">图像高度（行数）</param>
        /// <param name="centerRow">中心行索引，通常传 height/2</param>
        /// <param name="halfWindow">上下各取几行，默认2（共5行）</param>
        public static short[] ExtractDepthLineAvg(
            short[] depthData, int width, int height,
            int centerRow, int halfWindow = 2)
        {
            // 限制行范围
            int rowStart = Math.Max(0, centerRow - halfWindow);
            int rowEnd = Math.Min(height - 1, centerRow + halfWindow);

            short[] result = new short[width];

            for (int col = 0; col < width; col++)
            {
                long sum = 0;
                int count = 0;

                for (int row = rowStart; row <= rowEnd; row++)
                {
                    short val = depthData[row * width + col];
                    if (val > 0)   // 只累加有效值
                    {
                        sum += val;
                        count++;
                    }
                }

                result[col] = count > 0 ? (short)(sum / count) : (short)0;
            }

            return result;
        }
    }
}