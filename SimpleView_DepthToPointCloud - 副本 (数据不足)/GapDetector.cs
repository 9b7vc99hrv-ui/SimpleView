using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleView_DepthToPointCloud
{
    /// <summary>
    /// 卷线缝隙检测 + MAD (Modified Z-score) 计算
    /// </summary>
    public class GapDetector
    {
        // 检测结果
        public class GapResult
        {
            public double MadValue { get; set; }           // 计算的MAD值
            public double ModifiedZScore { get; set; }     // 改良Z值
            public bool IsAnomaly { get; set; }            // 是否异常
            public double AvgGapMm { get; set; }           // 平均缝隙间距(mm)
            public int GapCount { get; set; }              // 检测到的缝隙数量
            public List<double> GapDistances { get; set; } // 所有缝隙间距(mm)
            public bool HasValidData { get; set; }         // 是否有有效数据
        }

        // 配置参数
        public double ThresholdZ { get; set; }      // Z值阈值
        public double MinGapDistancePixels { get; set; }  // 最小缝隙间距(像素)
        public int MedianWindowSize { get; set; }          // 中值滤波窗口大小
        public double MmPerPixel { get; set; }         // X方向每像素对应的毫米数，需要根据实际标定调整

        // 用于MAD历史统计
        private List<double> m_historyGaps;
        private const int MAX_HISTORY = 100;

        public GapDetector()
        {
            ThresholdZ = 5.0;
            MinGapDistancePixels = 10;
            MedianWindowSize = 5;
            MmPerPixel = 0.02;
            m_historyGaps = new List<double>();
        }

        /// <summary>
        /// 处理深度数据
        /// </summary>
        /// <param name="depthData">深度数据数组(short)，来自深度图的一行</param>
        /// <param name="width">该行数据宽度</param>
        /// <returns>检测结果</returns>
        public GapResult ProcessDepthLine(short[] depthData, int width)
        {
            GapResult result = new GapResult();
            result.GapDistances = new List<double>();
            result.HasValidData = false;

            if (depthData == null || width < 10)
                return result;

            // 1. 提取有效的深度数据（忽略无效点，比如0或极大值）
            float[] validProfile = PreprocessDepthData(depthData, width);
            if (validProfile.Length < 10)
                return result;

            // 2. 找线筒基准面（全局最低深度）
            float spoolBase = FindSpoolBase(validProfile);

            // 3. 识别线材层区域（高于线筒的部分）
            int[] wireLayerIndices = FindWireLayerRegion(validProfile, spoolBase);
            if (wireLayerIndices.Length < 20)
                return result;

            // 4. 在线材层表面找波谷（缝隙）
            List<int> valleyIndices = FindValleys(validProfile, wireLayerIndices, spoolBase);
            if (valleyIndices.Count < 2)
                return result;

            // 5. 计算缝隙间距（像素 → 毫米）
            List<double> gapDistancesMm = new List<double>();
            for (int i = 1; i < valleyIndices.Count; i++)
            {
                double gapPixels = valleyIndices[i] - valleyIndices[i - 1];
                if (gapPixels >= MinGapDistancePixels)
                {
                    gapDistancesMm.Add(gapPixels * MmPerPixel);
                }
            }

            if (gapDistancesMm.Count < 1)
                return result;

            result.GapDistances = gapDistancesMm;
            result.GapCount = gapDistancesMm.Count;
            result.AvgGapMm = gapDistancesMm.Average();
            result.HasValidData = true;

            // 6. MAD + Modified Z-score 计算
            double median = Median(gapDistancesMm);
            double mad = MedianAbsoluteDeviation(gapDistancesMm, median);
            result.MadValue = mad;

            // Modified Z-score: 0.6745 * (x - median) / MAD
            double modifiedZScore = 0;
            if (mad > 0)
            {
                double maxDeviation = gapDistancesMm.Max() - median;
                modifiedZScore = 0.6745 * maxDeviation / mad;
            }
            result.ModifiedZScore = modifiedZScore;

            // 判断异常
            result.IsAnomaly = (modifiedZScore > ThresholdZ);

            return result;
        }

        /// <summary>
        /// 预处理深度数据：中值滤波、去除无效值
        /// </summary>
        private float[] PreprocessDepthData(short[] depthData, int width)
        {
            List<float> valid = new List<float>();

            for (int i = 0; i < width; i++)
            {
                short val = depthData[i];
                // 跳过无效值（0或负数通常表示无效测量）
                if (val < 10)
                    continue;
                valid.Add(val);
            }

            if (valid.Count < 10)
                return new float[0];

            // 中值滤波
            float[] smoothed = MedianFilter(valid.ToArray(), MedianWindowSize);
            return smoothed;
        }

        /// <summary>
        /// 中值滤波
        /// </summary>
        private float[] MedianFilter(float[] data, int windowSize)
        {
            if (windowSize < 3) return data;

            float[] result = new float[data.Length];
            int halfWindow = windowSize / 2;

            for (int i = 0; i < data.Length; i++)
            {
                List<float> window = new List<float>();
                for (int j = -halfWindow; j <= halfWindow; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < data.Length)
                        window.Add(data[idx]);
                }
                window.Sort();
                result[i] = window[window.Count / 2];
            }
            return result;
        }

        /// <summary>
        /// 寻找线筒基准面（全局最低深度区域）
        /// 取最低5%深度值的平均值作为线筒面
        /// </summary>
        private float FindSpoolBase(float[] profile)
        {
            float[] sorted = (float[])profile.Clone();
            Array.Sort(sorted);

            // 取最低5%的均值
            int count = Math.Max(1, sorted.Length / 20);
            float sum = 0;
            for (int i = 0; i < count; i++)
            {
                sum += sorted[i];
            }
            return sum / count;
        }

        /// <summary>
        /// 识别线材层区域（高于线筒一定阈值的区域）
        /// 线材堆积区域的深度值小于线筒基准面（数值越小表示越高）
        /// </summary>
        private int[] FindWireLayerRegion(float[] profile, float spoolBase)
        {
            // 线材层：深度值低于线筒基准面一定范围（spoolBase - 一个容差）
            // 深度值越小表示越靠近相机（越高），线筒是最低的（最大深度值）
            // 所以线材区域是 profile[i] < spoolBase（比线筒浅/高）
            // 但也要排除噪声点（太浅的异常点）
            
            float wireThreshold = spoolBase; // 线材区域：深度值低于这个值（更高）
            float noiseFloor = spoolBase * 0.3f; // 排除过高的噪声点

            List<int> indices = new List<int>();
            for (int i = 0; i < profile.Length; i++)
            {
                if (profile[i] < wireThreshold && profile[i] > noiseFloor)
                {
                    indices.Add(i);
                }
            }
            return indices.ToArray();
        }

        /// <summary>
        /// 在线材层表面找波谷（缝隙位置）
        /// 波谷 = 线材层表面相对较低的局部最小值
        /// </summary>
        private List<int> FindValleys(float[] profile, int[] wireIndices, float spoolBase)
        {
            // 将线材区域转换为连续区间
            List<int> valleys = new List<int>();

            if (wireIndices.Length < 5)
                return valleys;

            // 找局部最小值：比左右相邻点都低的点
            // 且深度值不能太接近线筒（避免找到线筒结构）
            float spoolMargin = spoolBase * 0.85f; // 低于这个值不算缝隙（太接近线筒）

            HashSet<int> wireSet = new HashSet<int>(wireIndices);

            for (int i = 1; i < profile.Length - 1; i++)
            {
                if (!wireSet.Contains(i))
                    continue;

                // 检查是否为局部最小值
                if (profile[i] < profile[i - 1] && profile[i] < profile[i + 1])
                {
                    // 排除太接近线筒的波谷（那是线筒结构，不是线缝隙）
                    if (profile[i] < spoolMargin)
                        continue;

                    // 排除太浅的微小波动（噪声）
                    float depthToNeighbors = Math.Min(
                        Math.Abs(profile[i] - profile[i - 1]),
                        Math.Abs(profile[i] - profile[i + 1]));
                    if (depthToNeighbors < 5) // 深度变化太小，可能是噪声
                        continue;

                    valleys.Add(i);
                }
            }

            // 合并相邻的波谷（取更深的那个）
            if (valleys.Count > 1)
            {
                List<int> merged = new List<int>();
                merged.Add(valleys[0]);

                for (int i = 1; i < valleys.Count; i++)
                {
                    if (valleys[i] - merged[merged.Count - 1] < 3)
                    {
                        // 相邻，取更深的（深度值更大的）
                        if (profile[valleys[i]] > profile[merged[merged.Count - 1]])
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

        /// <summary>
        /// 计算中位数
        /// </summary>
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

        /// <summary>
        /// 计算Median Absolute Deviation
        /// </summary>
        private double MedianAbsoluteDeviation(List<double> values, double median)
        {
            List<double> deviations = new List<double>();
            foreach (double v in values)
            {
                deviations.Add(Math.Abs(v - median));
            }
            return Median(deviations);
        }

        /// <summary>
        /// 从深度图数据(short数组)中提取一行深度数据
        /// </summary>
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
        /// 从点云数据(float数组)中提取指定行的Z值
        /// 点云数据格式: [X0,Y0,Z0, X1,Y1,Z1, ...]
        /// </summary>
        public static float[] ExtractPointCloudZLine(float[] pointCloudData, int width, int height, int rowIndex)
        {
            if (rowIndex < 0) rowIndex = 0;
            if (rowIndex >= height) rowIndex = height - 1;

            float[] zLine = new float[width];
            int offset = rowIndex * width * 3; // 每点3个float
            for (int i = 0; i < width; i++)
            {
                zLine[i] = pointCloudData[offset + i * 3 + 2]; // Z值
            }
            return zLine;
        }
    }
}
