using ExamenSecurity.Api.DTOs;

namespace ExamenSecurity.Api.Services;

public interface IDemoScenarioService
{
    IReadOnlyList<DemoScenarioResponse> GetScenarios();
    VulnerableObservabilitySummaryResponse GetVulnerableSummary();
}
