namespace MicroSaaS.Core.Entities;

/// <summary>
/// Representa el estado actual de una suscripción al SaaS.
/// </summary>
public enum SubscriptionStatus
{
    Active = 1,
    PastDue = 2,
    Suspended = 3
}

