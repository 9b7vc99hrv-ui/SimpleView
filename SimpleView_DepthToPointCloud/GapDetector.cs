using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleView_DepthToPointCloud
{
    /// <summary>
    /// 卷线缝隙检测 + MAD (Modified Z-score) 计算
    /// 
    /// 检测原理：
    /// 线激光传感器的深度轮廓是一条连续的线。
    /// 卷线的线材在轮廓上表现为起伏的波峰和波谷，
    /// 波峰 = 线材（较高的位置，靠近传感器）
    /// 波谷 = 缝隙（线材之间的凹槽）
    /// 
    /// 以最上层（最高波峰）为基准，往下找深度波谷作为缝隙位置。
    /// </summary>
    public class GapDetector
    {
        // 检测结果
        public class GapResult
        {
            public double MadValue { get; set; }
            public double ModifiedZScore { get; set; }
            public bool IsAnomaly { get; set; }
            public double AvgGapMm { get; set; }
            public int GapCount { get; set; }
            public List<double> GapDistancesMm { get; set; }
            public bool HasValidData { get; set; }
        }

        // 配置参数
        public double ThresholdZ { get; set; }
        public double MmPerPixel { get; set; }

        public GapDetector()
        {
            ThresholdZ = 5.0;
            MmPerPixel = 0.02;
        }

        /// <summary>
        /// 处理深度数据 - 通过查找轮廓波谷来检测缝隙
        /// </summary>
        public GapResult ProcessDepthLine(short[] depthData, int width)
        {
            GapResult result = new GapResult();
            result.GapDistancesMm = new List<double>();
            result.HasValidData = false;

            if (depthData == null || width < 20)
                return result;

            // 1. 预处理：中值滤波去噪，同时保留原始数据索引
            float[] smoothed = Preprocess(depthData, width);
            if (smoothed == null || smoothed.Length < 20)
                return result;

            // 2. 找到最上层基准（最高波峰的深度值）
            //    深度值越小 = 越靠近传感器 = 越高
            float topLayerDepth = float.MaxValue;
            for (int i = 0; i < smoothed.Length; i++)
            {
                if (smoothed[i] > 0 && smoothed[i] < topLayerDepth)
                {
                    topLayerDepth = smoothed[i];
                }
            }

            if (topLayerDepth >= float.MaxValue)
                return result;

            // 3. 计算波谷深度阈值 = 最上层 + 偏移量
            //    在一定深度范围内的波谷才算缝隙
            float valleyThreshold = topLayerDepth + 300; // 可调参数

            // 4. 找所有局部最小值（波谷）
            List<Valley> valleys = FindValleys(smoothed, valleyThreshold);

            if (valleys.Count < 2)
                return result;

            // 5. 计算相邻波谷间距（像素 → 毫米）
            List<double> gapDistancesMm = new List<double>();
            for (int i = 1; i < valleys.Count; i++)
            {
                double gapPixels = valleys[i].Index - valleys[i - 1].Index;
                if (gapPixels >= 10) // 最小间距过滤噪声
                {
                    gapDistancesMm.Add(gapPixels * MmPerPixel);
                }
            }

            if (gapDistancesMm.Count < 1)
                return result;

            result.GapDistancesMm = gapDistancesMm;
            result.GapCount = gapDistancesMm.Count;
            result.AvgGapMm = gapDistancesMm.Average();
            result.HasValidData = true;

            // 6. MAD + Modified Z-score
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

            return result;
        }

        /// <summary>
        /// 波谷数据结构
        /// </summary>
        private class Valley
        {
            public int Index { get; set; }
            public float Depth { get; set; }
        }

        /// <summary>
        /// 预处理：中值滤波 + 去除无效值
        /// </summary>
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
                    result[i] = -1; // 无效标记
                }
            }

            if (validCount < 20)
                return null;

            // 简单中值滤波（窗口=3）
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

        /// <summary>
        /// 查找波谷（局部最小值）
        /// 波谷条件：
        /// 1. 是有效数据点
        /// 2. 比左右相邻点都低（局部最小值）
        /// 3. 深度值在一定范围内（排除线筒底层）
        /// </summary>
        private List<Valley> FindValleys(float[] profile, float valleyThreshold)
        {
            List<Valley> valleys = new List<Valley>();
            int n = profile.Length;

            // 滑动窗口找局部最小值
            // 窗口大小 = 5（左右各2个点）
            for (int i = 2; i < n - 2; i++)
            {
                if (profile[i] < 0) continue;     // 无效点
                if (profile[i] > valleyThreshold) continue; // 太深了，可能是线筒

                // 检查是否是局部最小值（比左右各2个点都低）
                bool isValley = true;
                for (int j = -2; j <= 2; j++)
                {
                    if (j == 0) continue;
                    int idx = i + j;
                    if (idx >= 0 && idx < n && profile[idx] > 0)
                    {
                        if (profile[i] >= profile[idx]) // 如果不是严格更低
                        {
                            isValley = false;
                            break;
                        }
                    }
                }

                if (isValley)
                {
                    valleys.Add(new Valley { Index = i, Depth = profile[i] });
                }
            }

            // 合并相邻的波谷（取更深的那个）
            if (valleys.Count > 1)
            {
                List<Valley> merged = new List<Valley>();
                merged.Add(valleys[0]);

                for (int i = 1; i < valleys.Count; i++)
                {
                    if (valleys[i].Index - merged[merged.Count - 1].Index <= 3)
                    {
                        // 相邻，取更深的（深度值更大的）
                        if (valleys[i].Depth > merged[merged.Count - 1].Depth)
                        {
                            merged[merged.Count - 1] = valleys[i];
                        }
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
            {
                deviations.Add(Math.Abs(v - median));
            }
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
    }
}
