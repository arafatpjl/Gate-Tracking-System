namespace GtrackWeb.Configuration;

/// <summary>
/// Strongly-typed application settings, the web equivalent of the desktop
/// app.config appSettings (ServerType, ReportPath, company info, GP year).
/// </summary>
public class GtrackSettings
{
    public const string SectionName = "GtrackSettings";

    public string ServerType { get; set; } = "MSSQL";

    /// <summary>Financial / gate-pass year used when numbering GPs (desktop: Extra.call.Year).</summary>
    public string GpYear { get; set; } = DateTime.Now.Year.ToString();

    public string ReportPath { get; set; } = string.Empty;

    public CompanyHeader Company { get; set; } = new();

    public class CompanyHeader
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
    }
}
