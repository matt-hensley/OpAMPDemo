using System.Globalization;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;

namespace OpAMPDemo;

/// <summary>
/// Samples traces according to the specified probability.
/// </summary>
public sealed class OpAmpSampler : Sampler, IDisposable
{
    private readonly long idUpperBound;
    private double probability;
    private IDisposable? disposeOnChange;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpAmpSampler"/> class.
    /// </summary>
    /// <param name="config">The current OpAmp settings, including sampleRatio</param>
    public OpAmpSampler(IOptionsMonitor<OpAmpConfig> config)
    {
        //Guard.ThrowIfOutOfRange(probability, min: 0.0, max: 1.0);
        if (config == null) throw new NullReferenceException(nameof(config));

        this.probability = config.CurrentValue.sampleRatio ?? 1.0;
        disposeOnChange = config.OnChange((c, _) =>
        {
            this.probability = c.sampleRatio ?? 1.0;
        });

        // The expected description is like OpAmpSampler{0.000100}
        this.Description = "OpAmpSampler{" + this.probability.ToString("F6", CultureInfo.InvariantCulture) + "}";

        // Special case the limits, to avoid any possible issues with lack of precision across
        // double/long boundaries. For probability == 0.0, we use Long.MIN_VALUE as this guarantees
        // that we will never sample a trace, even in the case where the id == Long.MIN_VALUE, since
        // Math.Abs(Long.MIN_VALUE) == Long.MIN_VALUE.
        if (this.probability == 0.0)
        {
            this.idUpperBound = long.MinValue;
        }
        else if (this.probability == 1.0)
        {
            this.idUpperBound = long.MaxValue;
        }
        else
        {
            this.idUpperBound = (long)(probability * long.MaxValue);
        }
    }

    /// <inheritdoc />
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        // Always sample if we are within probability range. This is true even for child activities (that
        // may have had a different sampling decision made) to allow for different sampling policies,
        // and dynamic increases to sampling probabilities for debugging purposes.
        // Note use of '<' for comparison. This ensures that we never sample for probability == 0.0,
        // while allowing for a (very) small chance of *not* sampling if the id == Long.MAX_VALUE.
        // This is considered a reasonable trade-off for the simplicity/performance requirements (this
        // code is executed in-line for every Activity creation).
        Span<byte> traceIdBytes = stackalloc byte[16];
        samplingParameters.TraceId.CopyTo(traceIdBytes);
        return new SamplingResult(Math.Abs(GetLowerLong(traceIdBytes)) < this.idUpperBound);
    }

    private static long GetLowerLong(ReadOnlySpan<byte> bytes)
    {
        long result = 0;
        for (var i = 0; i < 8; i++)
        {
            result <<= 8;
#pragma warning disable CS0675 // Bitwise-or operator used on a sign-extended operand
            result |= bytes[i] & 0xff;
#pragma warning restore CS0675 // Bitwise-or operator used on a sign-extended operand
        }

        return result;
    }

    public void Dispose()
    {
        disposeOnChange?.Dispose();
    }
}
