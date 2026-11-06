namespace MicroSaaS.Core.Enums;

/// <summary>
/// Estados del ciclo de vida de un Documento Tributario Electrónico (DTE) ante el SII.
/// </summary>
public enum DteStatus
{
    Pending = 1,
    Signed = 2,
    SentToSii = 3,
    Accepted = 4,
    Rejected = 5
}

