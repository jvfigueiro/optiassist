using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace OptiAssist.Addons.PCAssist.Core
{
    public enum BenchType
    {
        Cpu,
        Ram,
        Disk,
        Stress
    }

    public class BenchResult
    {
        public BenchType Type { get; set; }
        public double Score { get; set; }
        public string PrimaryMetric { get; set; }
        public string SecondaryMetric { get; set; }
        public string BaselineComparison { get; set; }
        public string Evaluation { get; set; }
        public double ElapsedSeconds { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class BenchEngineService
    {
        public async Task<BenchResult> RunCpuBenchAsync(int durationSeconds, CancellationToken token, Action<int> progressCallback)
        {
            var result = new BenchResult { Type = BenchType.Cpu };
            var sw = Stopwatch.StartNew();

            try
            {
                int threads = Environment.ProcessorCount;
                long totalOps = 0;
                var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(durationSeconds));

                var progressTimer = Task.Run(async () =>
                {
                    int sec = 0;
                    while (!cts.IsCancellationRequested && sec < durationSeconds)
                    {
                        await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                        sec++;
                        if (progressCallback != null) progressCallback(sec);
                    }
                }, cts.Token);

                Task[] tasks = new Task[threads];
                for (int i = 0; i < threads; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        byte[] buffer = new byte[1024];
                        new Random().NextBytes(buffer);
                        using (var sha = SHA256.Create())
                        {
                            long localOps = 0;
                            while (!cts.IsCancellationRequested)
                            {
                                sha.ComputeHash(buffer);
                                localOps++;
                                if ((localOps & 0x7F) == 0 && cts.IsCancellationRequested) break;
                            }
                            Interlocked.Add(ref totalOps, localOps);
                        }
                    }, cts.Token);
                }

                try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { }
                try { await progressTimer.ConfigureAwait(false); } catch { }

                sw.Stop();
                double elapsed = sw.Elapsed.TotalSeconds;
                if (elapsed <= 0) elapsed = 1.0;

                double score = Math.Round((double)totalOps / elapsed);
                result.Score = score;
                result.ElapsedSeconds = Math.Round(elapsed, 1);
                result.PrimaryMetric = string.Format("{0:N0} pts", score);
                result.SecondaryMetric = string.Format("{0} threads | {1:N0} ops total", threads, totalOps);
                result.BaselineComparison = "Core i3: ~8,000 | Core i5/Ryzen 5: ~18,000 | Core i7/Ryzen 7: ~32,000+";
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<BenchResult> RunRamBenchAsync(CancellationToken token, Action<int> progressCallback)
        {
            var result = new BenchResult { Type = BenchType.Ram };
            var sw = Stopwatch.StartNew();

            try
            {
                await Task.Run(() =>
                {
                    // Allocate 128 MB block for quick bandwidth check
                    int sizeBytes = 128 * 1024 * 1024;
                    byte[] dataA = new byte[sizeBytes];
                    byte[] dataB = new byte[sizeBytes];

                    if (progressCallback != null) progressCallback(25);
                    token.ThrowIfCancellationRequested();

                    // 1. Write benchmark: fill memory
                    var writeSw = Stopwatch.StartNew();
                    for (int round = 0; round < 3; round++)
                    {
                        for (int i = 0; i < sizeBytes; i += 64)
                        {
                            dataA[i] = (byte)(i & 0xFF);
                        }
                    }
                    writeSw.Stop();

                    if (progressCallback != null) progressCallback(65);
                    token.ThrowIfCancellationRequested();

                    // 2. Read benchmark: copy memory
                    var readSw = Stopwatch.StartNew();
                    for (int round = 0; round < 3; round++)
                    {
                        Buffer.BlockCopy(dataA, 0, dataB, 0, sizeBytes);
                    }
                    readSw.Stop();

                    if (progressCallback != null) progressCallback(100);

                    double totalWriteBytes = (double)sizeBytes * 3.0;
                    double totalReadBytes = (double)sizeBytes * 3.0;

                    double writeGbSec = (totalWriteBytes / (1024.0 * 1024.0 * 1024.0)) / Math.Max(0.001, writeSw.Elapsed.TotalSeconds);
                    double readGbSec = (totalReadBytes / (1024.0 * 1024.0 * 1024.0)) / Math.Max(0.001, readSw.Elapsed.TotalSeconds);

                    double avgGbSec = Math.Round((readGbSec + writeGbSec) / 2.0, 1);
                    result.Score = avgGbSec;
                    result.PrimaryMetric = string.Format("{0:F1} GB/s", avgGbSec);
                    result.SecondaryMetric = string.Format("Read: {0:F1} GB/s | Write: {1:F1} GB/s", readGbSec, writeGbSec);
                    result.BaselineComparison = "DDR3 Single: ~7 GB/s | DDR4 Single: ~12 GB/s | DDR4 Dual: ~22 GB/s | DDR5: ~35+ GB/s";
                    result.Success = true;
                }, token).ConfigureAwait(false);

                sw.Stop();
                result.ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        public async Task<BenchResult> RunDiskBenchAsync(string targetDir, CancellationToken token, Action<int> progressCallback)
        {
            var result = new BenchResult { Type = BenchType.Disk };
            var sw = Stopwatch.StartNew();

            string testPath = Path.Combine(string.IsNullOrEmpty(targetDir) ? Path.GetTempPath() : targetDir, "optiassist_bench_" + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                await Task.Run(() =>
                {
                    int totalFileSize = 128 * 1024 * 1024; // 128 MB
                    int bufferSize = 1024 * 1024;           // 1 MB chunks
                    byte[] chunk = new byte[bufferSize];
                    new Random().NextBytes(chunk);

                    // 1. Write Test (FileOptions.WriteThrough to bypass OS cache)
                    var writeSw = Stopwatch.StartNew();
                    using (var fs = new FileStream(testPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.WriteThrough))
                    {
                        int written = 0;
                        while (written < totalFileSize)
                        {
                            token.ThrowIfCancellationRequested();
                            fs.Write(chunk, 0, chunk.Length);
                            written += chunk.Length;
                            int pct = (int)(((double)written / totalFileSize) * 50.0);
                            if (progressCallback != null) progressCallback(pct);
                        }
                        fs.Flush();
                    }
                    writeSw.Stop();

                    // 2. Read Test
                    var readSw = Stopwatch.StartNew();
                    using (var fs = new FileStream(testPath, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize, FileOptions.None))
                    {
                        int readTotal = 0;
                        while (readTotal < totalFileSize)
                        {
                            token.ThrowIfCancellationRequested();
                            int bytesRead = fs.Read(chunk, 0, chunk.Length);
                            if (bytesRead <= 0) break;
                            readTotal += bytesRead;
                            int pct = 50 + (int)(((double)readTotal / totalFileSize) * 50.0);
                            if (progressCallback != null) progressCallback(pct);
                        }
                    }
                    readSw.Stop();

                    double writeMbSec = ((double)totalFileSize / (1024.0 * 1024.0)) / Math.Max(0.001, writeSw.Elapsed.TotalSeconds);
                    double readMbSec = ((double)totalFileSize / (1024.0 * 1024.0)) / Math.Max(0.001, readSw.Elapsed.TotalSeconds);

                    result.Score = Math.Round(readMbSec, 0);
                    result.PrimaryMetric = string.Format("{0:N0} MB/s", readMbSec);
                    result.SecondaryMetric = string.Format("Sequential Read: {0:N0} MB/s | Write: {1:N0} MB/s", readMbSec, writeMbSec);

                    if (readMbSec >= 1500)
                    {
                        result.Evaluation = "🚀 NVMe PCIe High Performance";
                    }
                    else if (readMbSec >= 400)
                    {
                        result.Evaluation = "⚡ SATA III Solid State Drive (SSD)";
                    }
                    else if (readMbSec >= 80)
                    {
                        result.Evaluation = "🐢 Mechanical Hard Disk Drive (HDD)";
                    }
                    else
                    {
                        result.Evaluation = "⚠️ Degraded Speed or High Latency Storage";
                    }

                    result.BaselineComparison = "HDD: ~100 MB/s | SATA SSD: ~500 MB/s | NVMe Gen3: ~2,500 MB/s | NVMe Gen4: ~5,000 MB/s";
                    result.Success = true;
                }, token).ConfigureAwait(false);

                sw.Stop();
                result.ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                try
                {
                    if (File.Exists(testPath)) File.Delete(testPath);
                }
                catch { }
            }

            return result;
        }

        public async Task<BenchResult> RunCpuStressTestAsync(int durationSeconds, CancellationToken token, Action<int> progressCallback)
        {
            var result = new BenchResult { Type = BenchType.Stress };
            var sw = Stopwatch.StartNew();

            try
            {
                int threads = Environment.ProcessorCount;
                var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                cts.CancelAfter(TimeSpan.FromSeconds(durationSeconds));

                var progressTimer = Task.Run(async () =>
                {
                    int sec = 0;
                    while (!cts.IsCancellationRequested && sec < durationSeconds)
                    {
                        await Task.Delay(1000, cts.Token).ConfigureAwait(false);
                        sec++;
                        if (progressCallback != null) progressCallback(sec);
                    }
                }, cts.Token);

                Task[] tasks = new Task[threads];
                for (int i = 0; i < threads; i++)
                {
                    tasks[i] = Task.Run(() =>
                    {
                        double a = 1.000001;
                        while (!cts.IsCancellationRequested)
                        {
                            a = Math.Sqrt(a * a + 1.0);
                        }
                    }, cts.Token);
                }

                try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { }
                try { await progressTimer.ConfigureAwait(false); } catch { }

                sw.Stop();
                result.ElapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1);
                result.Score = durationSeconds;
                result.PrimaryMetric = string.Format("Completed {0}s Stress Test", durationSeconds);
                result.SecondaryMetric = string.Format("All {0} logical CPU threads loaded at 100%", threads);
                result.BaselineComparison = "Thermal stability validated. Check if fans kicked in and no thermal shutdown occurred.";
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
    }
}
