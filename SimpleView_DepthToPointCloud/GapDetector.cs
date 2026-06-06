using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleView_DepthToPointCloud
{
    /// <summary>
    /// 卷线缝隙检测 - 基于预设规格判断绕线是否正常
    /// 正常条件：所有约束同时满足
    /// 异常条件：任意一个约束被违反
    /// </summary>
    public class GapDetector
    {
        // =====================================================================
        // 检测结果
        // =====================================================================
        public class GapResult
        {
            public double MadValue { get; set; }
            public double ModifiedZScore { get; set; }
            public bool IsAnomaly { get; set; }
            public double AvgGapMm { get; set; }
            public int GapCount { get; set; }
            public List<double> GapDistancesMm { get; set; }
            public bool HasValidData { get; set; }

            public int InferredTurn { get; set; }           // 检测到的峰数（匝数）
            public int CurrentLayer { get; set; } = -1;     // 当前层数（基于基准推算，0开始）
            public bool IsLayerCompleted { get; set; }      // 当前层是否完成（峰数 ≥ N）
            public List<string> AnomalyReasons { get; set; } = new List<string>();
        }

        // =====================================================================
        // 配置参数
        // =====================================================================

        public double ThresholdZ { get; set; } = 5.0;
        public double MmPerPixel { get; set; } = 0.02;
        public float WireWidth { get; set; } = 4.10f;
        public float WireHeight { get; set; } = 1.70f;
        public int TurnsPerLayer { get; set; } = 8;
        public int TotalLayers { get; set; } = 9;

        public float HeightTolerance { get; set; } = 0.30f;
        public float SpacingMinRatio { get; set; } = 0.70f;
        public float SpacingMaxRatio { get; set; } = 1.50f;
        public float WidthMinRatio { get; set; } = 0.90f;
        public float WidthMaxRatio { get; set; } = 1.10f;

        // 基准标定
        private float m_calibratedAvgDepthRaw = 0f;
        private bool m_isCalibrated = false;

        // 峰检测模式：true=找局部最小值(波谷)，false=找局部最大值(波峰)
        // 默认使用波谷（depth越小表示高度越高，线材顶部是波谷）
        // 如果调试发现不对，可以修改这个值
        private bool m_useValleyForPeak = false;

        // =====================================================================
        // 主入口
        // =====================================================================
        public GapResult ProcessDepthLine(short[] depthData, int width)
        {
            GapResult result = new GapResult();
            result.GapDistancesMm = new List<double>();
            result.HasValidData = false;
            result.AnomalyReasons.Clear();

            if (depthData == null || width < 20)
                return result;

            float[] smoothed = Preprocess(depthData, width);
            if (smoothed == null || smoothed.Length < 20)
                return result;

            float mmPerPt = (float)MmPerPixel;
            var peaks = FindAllPeaks(smoothed, mmPerPt);

            if (peaks.Count == 0)
            {
                result.InferredTurn = 0;
                result.IsAnomaly = true;
                result.AnomalyReasons.Add("未检测到有效峰");
                result.HasValidData = true;
                return result;
            }

            int peakCount = peaks.Count;
            result.InferredTurn = peakCount;
            result.HasValidData = true;

            // 推断层数
            if (m_isCalibrated)
            {
                float currentAvgDepth = peaks.Average(p => p.Depth);
                result.CurrentLayer = InferCurrentLayer(currentAvgDepth);
            }

            result.IsLayerCompleted = (peakCount >= TurnsPerLayer);

            // 骑线检测
            float avgDepth = peaks.Average(p => p.Depth);
            float heightThreshRaw = WireHeight * HeightTolerance * 100f;

            for (int i = 0; i < peakCount; i++)
            {
                float deviation =  peaks[i].Depth - avgDepth;
                if (deviation > heightThreshRaw)
                {
                    result.IsAnomaly = true;
                    result.AnomalyReasons.Add($"骑线: 第{i + 1}匝 高出{deviation / 100f:F2}mm");
                }
            }

            // 间距检测
            float expectedSpacingMm = WireWidth;
            for (int i = 1; i < peakCount; i++)
            {
                float spacingMm = peaks[i].PositionMm - peaks[i - 1].PositionMm;

                if (spacingMm < expectedSpacingMm * SpacingMinRatio)
                {
                    result.IsAnomaly = true;
                    result.AnomalyReasons.Add($"压线: 第{i}-{i + 1}匝 间距{spacingMm:F2}mm < {expectedSpacingMm * SpacingMinRatio:F2}mm");
                }
                else if (spacingMm > expectedSpacingMm * SpacingMaxRatio)
                {
                    result.IsAnomaly = true;
                    result.AnomalyReasons.Add($"跨线: 第{i}-{i + 1}匝 间距{spacingMm:F2}mm > {expectedSpacingMm * SpacingMaxRatio:F2}mm");
                }
            }

            // 层完成时才判断缺线和卷宽
            if (result.IsLayerCompleted)
            {
                if (peakCount < TurnsPerLayer)
                {
                    result.IsAnomaly = true;
                    result.AnomalyReasons.Add($"缺线: 检测到{peakCount}匝，期望{TurnsPerLayer}匝");
                }

                if (peakCount >= 2)
                {
                    float totalWidthMm = peaks.Last().PositionMm - peaks.First().PositionMm;
                    float minWidth = (TurnsPerLayer - 1) * WireWidth * WidthMinRatio;
                    float maxWidth = (TurnsPerLayer + 1) * WireWidth * WidthMaxRatio;

                    if (totalWidthMm < minWidth || totalWidthMm > maxWidth)
                    {
                        result.IsAnomaly = true;
                        result.AnomalyReasons.Add($"卷宽异常: {totalWidthMm:F1}mm，正常范围({minWidth:F1}-{maxWidth:F1}mm)");
                    }
                }
            }

            ComputeMAD(result, peaks, mmPerPt);

            return result;
        }

        // =====================================================================
        // 峰检测
        // =====================================================================
        private class PeakInfo
        {
            public int Index { get; set; }
            public float Depth { get; set; }
            public float PositionMm { get; set; }
            public float Prominence { get; set; }
        }

        private List<PeakInfo> FindAllPeaks(float[] smoothed, float mmPerPt)
        {
            List<PeakInfo> peaks = new List<PeakInfo>();

            // 获取有效数据范围
            float minVal = float.MaxValue, maxVal = float.MinValue;
            int validCount = 0;
            for (int i = 0; i < smoothed.Length; i++)
            {
                if (smoothed[i] > 0)
                {
                    validCount++;
                    if (smoothed[i] < minVal) minVal = smoothed[i];
                    if (smoothed[i] > maxVal) maxVal = smoothed[i];
                }
            }

            if (validCount < 20) return peaks;

            float depthRange = maxVal - minVal;
            float minProminenceRaw = Math.Max(30, depthRange * 0.05f);
            int minDistPts = Math.Max(2, (int)(WireWidth * 0.5f / mmPerPt));

            for (int i = 3; i < smoothed.Length - 3; i++)
            {
                if (smoothed[i] <= 0) continue;

                bool isPeak;
                if (m_useValleyForPeak)
                {
                    // 找局部最小值（波谷）- 线材顶部
                    isPeak = smoothed[i] < smoothed[i - 1] && smoothed[i] < smoothed[i + 1] &&
                             smoothed[i] < smoothed[i - 2] && smoothed[i] < smoothed[i + 2] &&
                             smoothed[i] < smoothed[i - 3] && smoothed[i] < smoothed[i + 3];
                }
                else
                {
                    // 找局部最大值（波峰）- 线材顶部
                    isPeak = smoothed[i] > smoothed[i - 1] && smoothed[i] > smoothed[i + 1] &&
                             smoothed[i] > smoothed[i - 2] && smoothed[i] > smoothed[i + 2] &&
                             smoothed[i] > smoothed[i - 3] && smoothed[i] > smoothed[i + 3];
                }

                if (!isPeak) continue;

                // 计算 prominence
                int left = Math.Max(0, i - minDistPts);
                int right = Math.Min(smoothed.Length - 1, i + minDistPts);
                float localExtreme = smoothed[i];
                for (int k = left; k <= right; k++)
                {
                    if (m_useValleyForPeak)
                    {
                        if (smoothed[k] > localExtreme) localExtreme = smoothed[k];
                    }
                    else
                    {
                        if (smoothed[k] < localExtreme) localExtreme = smoothed[k];
                    }
                }
                float prominence = m_useValleyForPeak ? localExtreme - smoothed[i] : smoothed[i] - localExtreme;

                if (prominence < minProminenceRaw) continue;

                // 去重
                if (peaks.Count > 0 && (i - peaks.Last().Index) < minDistPts)
                {
                    bool keepNew = m_useValleyForPeak ? (smoothed[i] < peaks.Last().Depth) : (smoothed[i] > peaks.Last().Depth);
                    if (keepNew)
                    {
                        peaks[peaks.Count - 1] = new PeakInfo
                        {
                            Index = i,
                            Depth = smoothed[i],
                            PositionMm = i * mmPerPt,
                            Prominence = prominence
                        };
                    }
                    continue;
                }

                peaks.Add(new PeakInfo
                {
                    Index = i,
                    Depth = smoothed[i],
                    PositionMm = i * mmPerPt,
                    Prominence = prominence
                });
            }

            return peaks;
        }

        // =====================================================================
        // 基准标定
        // =====================================================================
        public void CalibrateBaseline(float[] smoothed, float mmPerPt)
        {
            var peaks = FindAllPeaks(smoothed, mmPerPt);
            if (peaks.Count > 0)
            {
                m_calibratedAvgDepthRaw = peaks.Average(p => p.Depth);
                m_isCalibrated = true;
            }
        }

        public void ClearCalibration()
        {
            m_isCalibrated = false;
            m_calibratedAvgDepthRaw = 0f;
        }

        public bool IsCalibrated => m_isCalibrated;

        public void SetPeakDetectionMode(bool useValley)
        {
            m_useValleyForPeak = useValley;
        }

        private int InferCurrentLayer(float currentAvgDepthRaw)
        {
            if (!m_isCalibrated) return -1;
            float diffRaw =  currentAvgDepthRaw - m_calibratedAvgDepthRaw;
            float diffMm = diffRaw / 100f;
            int layer = (int)Math.Round(diffMm / WireHeight);
            return Math.Max(0, Math.Min(layer, TotalLayers - 1));
        }

        // =====================================================================
        // 辅助方法
        // =====================================================================
        private void ComputeMAD(GapResult result, List<PeakInfo> peaks, float mmPerPt)
        {
            if (peaks.Count < 2)
            {
                result.MadValue = 0;
                result.ModifiedZScore = 0;
                return;
            }

            List<double> gapDistancesMm = new List<double>();
            for (int i = 1; i < peaks.Count; i++)
            {
                double gapPixels = peaks[i].Index - peaks[i - 1].Index;
                gapDistancesMm.Add(gapPixels * mmPerPt);
            }

            result.GapDistancesMm = gapDistancesMm;
            result.GapCount = gapDistancesMm.Count;

            if (gapDistancesMm.Count > 0)
            {
                result.AvgGapMm = gapDistancesMm.Average();
                double median = Median(gapDistancesMm);
                double mad = MedianAbsoluteDeviation(gapDistancesMm, median);
                result.MadValue = mad;

                if (mad > 0)
                {
                    double maxDeviation = gapDistancesMm.Max(g => Math.Abs(g - median));
                    result.ModifiedZScore = 0.6745 * maxDeviation / mad;
                }
            }
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

        public static short[] ExtractDepthLineAvg(short[] depthData, int width, int height, int centerRow, int halfWindow = 2)
        {
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
                    if (val > 0)
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