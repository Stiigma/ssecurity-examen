namespace ExamenSecurity.Api.DTOs;

public sealed record DemoScenarioResponse(
    string Id,
    string Title,
    string OwaspCategory,
    string WhatToDo,
    string ExpectedBehavior,
    string WhyItMatters);

public sealed record ObservabilitySummaryResponse(
    string Version,
    int PersistedSecurityEvents,
    int GeneratedSecurityAlerts,
    bool LoginFailuresAreAudited,
    bool AccessDeniedIsAudited,
    bool AdminActionsAreAudited,
    bool SuspiciousPatternsCreateAlerts,
    string PresentationMessage);
