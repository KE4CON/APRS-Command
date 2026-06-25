namespace Aprs.Services;

/// <summary>Where a packet is bound, for transmit-safety policy purposes.</summary>
public enum TransmitDestination
{
    /// <summary>Over the air via a radio / TNC port.</summary>
    Rf,

    /// <summary>To the APRS-IS internet network.</summary>
    AprsIs
}
