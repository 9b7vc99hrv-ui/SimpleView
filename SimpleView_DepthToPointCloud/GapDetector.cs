using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleView_DepthToPointCloud
{
    /// <summary>
    /// 卷线缝隙检测 + MAD (Modified Z-score) 计算
    /// 适用于点云为"中断线段"的场景：
    /// 线材在深度图中表现为一段段有效数据段，
    /// 缝隙 = 有效数据段之间的空白/无效数据区域
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
        public double MinSegmentLengthPixels { get; set; }
        public int MinGapPixels { get; set; }
        public double MmPerPixel { get; set; }

        public GapDetector()
        {
            ThresholdZ = 5.0;
            MinSegmentLengthPixels = 20;
            MinGapPixels = 5;
            MmPerPixel = 0.02;
        }

        /// <summary>
        /// 处理深度数据 - 通过查找数据段间隙来检测缝隙
        /// </summary>
        public GapResult ProcessDepthLine(short[] depthData, int width)
        {
            GapResult result = new GapResult();
            result.GapDistancesMm = new List<double>();
            result.HasValidData = false;

            if (depthData == null || width < 20)
                return result;

            // 1. 标记有效数据点 vs 无效数据点
            bool[] isValid = new bool[width];
            int validCount = 0;
            for (int i = 0; i < width; i++)
            {
                short val = depthData[i];
                isValid[i] = (val > 10);
                if (isValid[i]) validCount++;
            }

            if (validCount < 20)
                return result;

            // 2. 分割有效数据段
            List<DataSegment> segments = FindDataSegments(isValid, width);
            segments = segments.Where(s => s.Length >= MinSegmentLengthPixels).ToList();

            if (segments.Count < 2)
                return result;

            // 3. 计算相邻段之间的缝隙间距
            List<double> gapDistancesMm = CalcGapDistances(segments);
            if (gapDistancesMm.Count < 1)
                return result;

            // 4. 以最上层高度为基准过滤
            float topLayerDepth = FindTopLayerDepth(depthData, isValid, segments);
            List<DataSegment> topSegments = FilterTopLayerSegments(depthData, segments, topLayerDepth);

            if (topSegments.Count >= 2)
            {
                gapDistancesMm = CalcGapDistances(topSegments);
            }

            if (gapDistancesMm.Count < 1)
                return result;

            result.GapDistancesMm = gapDistancesMm;
            result.GapCount = gapDistancesMm.Count;
            result.AvgGapMm = gapDistancesMm.Average();
            result.HasValidData = true;

            // 5. MAD + Modified Z-score
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

        private class DataSegment
        {
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public int Length { get { return EndIndex - StartIndex + 1; } }
        }

        private List<DataSegment> FindDataSegments(bool[] isValid, int width)
        {
            List<DataSegment> segments = new List<DataSegment>();
            int i = 0;
            while (i < width)
            {
                while (i < width && !isValid[i]) i++;
                if (i >= width) break;

                int start = i;
                while (i < width && isValid[i]) i++;
                int end = i - 1;

                segments.Add(new DataSegment { StartIndex = start, EndIndex = end });
            }
            return segments;
        }

        private List<double> CalcGapDistances(List<DataSegment> segments)
        {
            List<double> gaps = new List<double>();
            for (int i = 1; i < segments.Count; i++)
            {
                int gapPixels = segments[i].StartIndex - segments[i - 1].EndIndex;
                if (gapPixels >= MinGapPixels)
                {
                    gaps.Add(gapPixels * MmPerPixel);
                }
            }
            return gaps;
        }

        private float FindTopLayerDepth(short[] depthData, bool[] isValid, List<DataSegment> segments)
        {
            float minDepth = float.MaxValue;
            foreach (DataSegment seg in segments)
            {
                for (int i = seg.StartIndex; i <= seg.EndIndex; i++)
                {
                    if (isValid[i] && depthData[i] < minDepth)
                    {
                        minDepth = depthData[i];
                    }
                }
            }
            return minDepth;
        }

        private List<DataSegment> FilterTopLayerSegments(
            short[] depthData, List<DataSegment> segments, float topLayerDepth)
        {
            float depthTolerance = 500;

            List<DataSegment> filtered = new List<DataSegment>();
            foreach (DataSegment seg in segments)
            {
                float sum = 0;
                int count = 0;
                for (int i = seg.StartIndex; i <= seg.EndIndex; i++)
                {
                    if (depthData[i] > 10)
                    {
                        sum += depthData[i];
                        count++;
                    }
                }
                if (count == 0) continue;
                float avgDepth = sum / count;

                if (avgDepth <= topLayerDepth + depthTolerance)
                {
                    filtered.Add(seg);
                }
            }

            return filtered;
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
