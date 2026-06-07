using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleView_DepthToPointCloud
{
    public class GapDetector
    {
        public class GapResult
        {
            public double MadValue { get; set; }
            public double ModifiedZScore { get; set; }
            public bool IsAnomaly { get; set; }
            public double AvgGapMm { get; set; }
            public int GapCount { get; set; }
            public List<double> GapDistancesMm { get; set; }
            public bool HasValidData { get; set; }
            public int InferredTurn { get; set; }
            public int CurrentLayer { get; set; } = -1;
            public bool IsLayerCompleted { get; set; }
            public List<string> AnomalyReasons { get; set; } = new List<string>();
        }

        public double ThresholdZ { get; set; } = 5.0;
        public double MmPerPixel { get; set; } = 0.0984375;
        public float WireWidth { get; set; } = 3.50f;
        public float WireHeight { get; set; } = 1.70f;
        public int TurnsPerLayer { get; set; } = 8;
        public int TotalLayers { get; set; } = 9;
        public float HeightTolerance { get; set; } = 0.30f;
        public float SpacingMinRatio { get; set; } = 0.70f;
        public float SpacingMaxRatio { get; set; } = 1.50f;
        public float WidthMinRatio { get; set; } = 0.90f;
        public float WidthMaxRatio { get; set; } = 1.10f;

        public float CoordZUnit { get; set; } = 670f / 65536f;
        public float ValleyRatio { get; set; } = 0.30f;

        private float m_calibratedAvgDepthRaw = 0f;
        private bool m_isCalibrated = false;
        private bool m_useValleyForPeak = true;

        public float MmToRaw(float mm)
        {
            return mm / CoordZUnit;
        }

        public GapResult ProcessDepthLine(float[] depthData, int width)
        {
            GapResult result = new GapResult();
            result.GapDistancesMm = new List<double>();
            result.HasValidData = false;
            result.AnomalyReasons.Clear();

            if (depthData == null || width < 20)
                return result;

            // ============================================================
            // 新增：检测数据波动范围，判断是否为空筒或无线材状态
            // ============================================================
            float minVal = float.MaxValue, maxVal = float.MinValue;
            int validCnt = 0;
            for (int i = 0; i < depthData.Length; i++)
            {
                if (depthData[i] != -1f)
                {
                    validCnt++;
                    if (depthData[i] < minVal) minVal = depthData[i];
                    if (depthData[i] > maxVal) maxVal = depthData[i];
                }
            }
            
            // 波动小于 80 raw (约0.8mm) 时，认为是空筒或无线材状态
            // 直接返回 0 匝，不进行后续检测
            if (validCnt > 0 && (maxVal - minVal) < 80f)
            {
                   // 添加这行，在输出窗口查看
                System.Diagnostics.Debug.WriteLine($"!!! 空筒判定触发: 波动={maxVal - minVal:F1} raw, 返回匝数=0 !!!");
                result.HasValidData = true;
                result.InferredTurn = 0;
                result.IsAnomaly = false;
                result.CurrentLayer = -1;
                result.IsLayerCompleted = false;
                return result;
            }

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

            if (m_isCalibrated)
            {
                float currentAvgDepth = peaks.Average(p => p.Depth);
                result.CurrentLayer = InferCurrentLayer(currentAvgDepth);
            }

            result.IsLayerCompleted = (peakCount >= TurnsPerLayer);

            float avgDepth = peaks.Average(p => p.Depth);
            float heightThreshRaw = MmToRaw(WireHeight) * HeightTolerance;

            for (int i = 0; i < peakCount; i++)
            {
                float deviation = Math.Abs(peaks[i].Depth - avgDepth);
                if (deviation > heightThreshRaw)
                {
                    result.IsAnomaly = true;
                    result.AnomalyReasons.Add($"骑线: 第{i + 1}匝 高度偏差{deviation * CoordZUnit:F2}mm");
                }
            }

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
            float minVal = float.MaxValue, maxVal = float.MinValue;
            int validCount = 0;
            for (int i = 0; i < smoothed.Length; i++)
            {
                if (smoothed[i] != -1f)
                {
                    validCount++;
                    if (smoothed[i] < minVal) minVal = smoothed[i];
                    if (smoothed[i] > maxVal) maxVal = smoothed[i];
                }
            }
            if (validCount < 20) return peaks;

            float depthRange = maxVal - minVal;
            
            // 阈值基于实际数据波动范围
            float minProminenceRaw = Math.Max(depthRange * 0.3f, 5f);
            float maxProminenceRaw = MmToRaw(WireHeight) * 0.5f;
            minProminenceRaw = Math.Min(minProminenceRaw, maxProminenceRaw);
            
            int minDistPts = Math.Max(2, (int)(WireWidth * 0.5f / mmPerPt));

            for (int i = 3; i < smoothed.Length - 3; i++)
            {
                if (smoothed[i] == -1f) continue;
                bool isPeak;
                if (m_useValleyForPeak)
                {
                    isPeak = smoothed[i] < smoothed[i - 1] && smoothed[i] < smoothed[i + 1] &&
                             smoothed[i] < smoothed[i - 2] && smoothed[i] < smoothed[i + 2] &&
                             smoothed[i] < smoothed[i - 3] && smoothed[i] < smoothed[i + 3];
                }
                else
                {
                    isPeak = smoothed[i] > smoothed[i - 1] && smoothed[i] > smoothed[i + 1] &&
                             smoothed[i] > smoothed[i - 2] && smoothed[i] > smoothed[i + 2] &&
                             smoothed[i] > smoothed[i - 3] && smoothed[i] > smoothed[i + 3];
                }
                if (!isPeak) continue;

                int left = Math.Max(0, i - minDistPts);
                int right = Math.Min(smoothed.Length - 1, i + minDistPts);
                float localExtreme = smoothed[i];
                for (int k = left; k <= right; k++)
                {
                    if (smoothed[k] == -1f) continue;
                    if (m_useValleyForPeak) { if (smoothed[k] > localExtreme) localExtreme = smoothed[k]; }
                    else { if (smoothed[k] < localExtreme) localExtreme = smoothed[k]; }
                }
                float prominence = m_useValleyForPeak ? localExtreme - smoothed[i] : smoothed[i] - localExtreme;
                if (prominence < minProminenceRaw) continue;

                if (peaks.Count > 0 && (i - peaks.Last().Index) < minDistPts)
                {
                    bool keepNew = m_useValleyForPeak ? (smoothed[i] < peaks.Last().Depth) : (smoothed[i] > peaks.Last().Depth);
                    if (keepNew)
                    {
                        peaks[peaks.Count - 1] = new PeakInfo { Index = i, Depth = smoothed[i], PositionMm = i * mmPerPt, Prominence = prominence };
                    }
                    continue;
                }
                peaks.Add(new PeakInfo { Index = i, Depth = smoothed[i], PositionMm = i * mmPerPt, Prominence = prominence });
            }
            return peaks;
        }

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
        public float CalibratedBaselineDepth => m_calibratedAvgDepthRaw;

        public void SetPeakDetectionMode(bool useValley)
        {
            m_useValleyForPeak = useValley;
        }

        private int InferCurrentLayer(float currentAvgDepthRaw)
        {
            if (!m_isCalibrated) return -1;
            float diffRaw = currentAvgDepthRaw - m_calibratedAvgDepthRaw;
            float diffMm = diffRaw * CoordZUnit;
            int layer = (int)Math.Round(diffMm / WireHeight);
            return Math.Max(0, Math.Min(layer, TotalLayers - 1));
        }

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

        private float[] Preprocess(float[] data, int width)
        {
            float[] result = new float[width];
            int validCount = 0;
            for (int i = 0; i < width; i++)
            {
                if (data[i] != -1f) { result[i] = data[i]; validCount++; }
                else result[i] = -1f;
            }
            if (validCount < 20) return null;
            float[] smoothed = new float[width];
            for (int i = 0; i < width; i++)
            {
                if (result[i] == -1f) { smoothed[i] = -1f; continue; }
                List<float> window = new List<float>();
                for (int j = -1; j <= 1; j++)
                {
                    int idx = i + j;
                    if (idx >= 0 && idx < width && result[idx] != -1f) window.Add(result[idx]);
                }
                if (window.Count > 0) { window.Sort(); smoothed[i] = window[window.Count / 2]; }
                else smoothed[i] = result[i];
            }
            return smoothed;
        }

        private double Median(List<double> values)
        {
            List<double> sorted = new List<double>(values);
            sorted.Sort();
            int n = sorted.Count;
            if (n % 2 == 0) return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
            else return sorted[n / 2];
        }

        private double MedianAbsoluteDeviation(List<double> values, double median)
        {
            List<double> deviations = new List<double>();
            foreach (double v in values) deviations.Add(Math.Abs(v - median));
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
                    if (val > 0) { sum += val; count++; }
                }
                result[col] = count > 0 ? (short)(sum / count) : (short)0;
            }
            return result;
        }
    }
}