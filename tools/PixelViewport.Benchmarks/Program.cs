using System.Diagnostics;
using PixelViewport.Imaging;

const int width = 1920;
const int height = 1080;
const int iterations = 30;

Console.WriteLine("PixelViewport deterministic conversion probe");
Console.WriteLine($"Runtime: {Environment.Version}; OS: {Environment.OSVersion}");
Console.WriteLine($"Frame: {width}x{height}; measured iterations: {iterations}");
Console.WriteLine("This measures the community CPU conversion path, not display latency or GPU rendering.\n");

MeasureConversion(ImagePixelFormat.Bgra32);
MeasureConversion(ImagePixelFormat.Gray16LittleEndian);
MeasureMailbox();

void MeasureConversion(ImagePixelFormat format)
{
    int sourceStride = format.GetMinimumStride(width);
    byte[] source = GC.AllocateUninitializedArray<byte>(sourceStride * height);
    new Random(1729).NextBytes(source);
    byte[] destination = GC.AllocateUninitializedArray<byte>(width * height * 4);
    using PixelFrame frame = PixelFrame.Wrap(source, width, height, sourceStride, format);

    for (int i = 0; i < 3; i++)
    {
        PixelFrameConverter.CopyToBgra32(frame, destination, width * 4);
    }

    var samples = new double[iterations];
    for (int i = 0; i < samples.Length; i++)
    {
        long start = Stopwatch.GetTimestamp();
        PixelFrameConverter.CopyToBgra32(frame, destination, width * 4);
        samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    Array.Sort(samples);
    double median = samples[samples.Length / 2];
    double p95 = samples[(int)Math.Ceiling(samples.Length * 0.95) - 1];
    double megapixelsPerSecond = (width * height / 1_000_000d) / (median / 1000d);
    Console.WriteLine($"{format,-24} median {median,7:F2} ms  p95 {p95,7:F2} ms  {megapixelsPerSecond,7:F1} MP/s");
}

void MeasureMailbox()
{
    const int submissions = 10_000;
    using var mailbox = new LatestFrameMailbox();
    byte[] pixel = [0, 0, 0, 255];
    long start = Stopwatch.GetTimestamp();
    for (int i = 0; i < submissions; i++)
    {
        mailbox.Submit(PixelFrame.Wrap(pixel, 1, 1, 4, ImagePixelFormat.Bgra32, i));
    }

    TimeSpan elapsed = Stopwatch.GetElapsedTime(start);
    FrameMailboxStatistics statistics = mailbox.Statistics;
    using PixelFrame? latest = mailbox.TryTake(out PixelFrame? frame) ? frame : null;
    Console.WriteLine(
        $"Mailbox burst            {submissions:N0} submits in {elapsed.TotalMilliseconds:F2} ms; " +
        $"dropped {statistics.Dropped:N0}; delivered sequence {latest?.SequenceNumber:N0}");
}
