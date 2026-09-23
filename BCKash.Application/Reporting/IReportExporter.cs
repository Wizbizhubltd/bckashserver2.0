using BCKash.Domain.Reporting;

namespace BCKash.Application.Reporting;

/// <summary>Renders a generic <see cref="ReportResult"/> to one of the three formats FR-RPT-2 requires.</summary>
public interface IReportExporter
{
    byte[] Export(ReportResult result, ReportSchedulerFileFormat format);

    string ContentType(ReportSchedulerFileFormat format);

    string FileExtension(ReportSchedulerFileFormat format);
}
