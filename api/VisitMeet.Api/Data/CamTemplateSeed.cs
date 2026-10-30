using VisitMeet.Api.Models;
using VisitMeet.Api.Services.Assessment;

namespace VisitMeet.Api.Data;

public static class CamTemplateSeed
{
    public static void Seed(AppDbContext db, Guid adminId, Guid lapId, DateTimeOffset now)
    {
        CamTemplateCatalog.Validate();
        var template = new OutputTemplate
        {
            Id = CamTemplateCatalog.BusinessLapTemplateId,
            Code = "business-lap-cam",
            Name = "Business LAP CAM",
            MemoType = "Credit Assessment Memo",
            Version = 2,
            Status = TemplateStatuses.Active,
            LoanCategoryIdsJson = AssessmentMapper.ToJson(new[] { lapId }),
            FileCategoriesJson = AssessmentMapper.ToJson(CamTemplateCatalog.FileCategories),
            CreatedByUserId = adminId,
            UpdatedByUserId = adminId,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.OutputTemplates.Add(template);

        var sectionIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var section in CamTemplateCatalog.Sections)
        {
            var id = Guid.NewGuid();
            sectionIds[section.Name] = id;
            db.OutputSections.Add(new OutputSection
            {
                Id = id,
                TemplateId = template.Id,
                Name = section.Name,
                Source = section.Source,
                SortOrder = section.Order
            });
        }

        foreach (var field in CamTemplateCatalog.Fields)
        {
            db.OutputFields.Add(new OutputField
            {
                Id = Guid.NewGuid(),
                TemplateId = template.Id,
                SectionId = sectionIds[field.Section],
                Number = field.Number,
                Label = field.Label,
                Question = field.Question,
                Datatype = field.Datatype,
                OptionsJson = AssessmentMapper.ToJson(field.Options),
                AllowMultiple = field.AllowMultiple,
                ConditionFieldNumber = field.WhenField,
                ConditionChoicesJson = AssessmentMapper.ToJson(field.WhenValues),
                Source = field.Source,
                PhotoType = field.PhotoType,
                TaskNumber = field.TaskNumber,
                RuleLink = field.RuleLink,
                JsonShape = field.JsonShape,
                SortOrder = field.Number
            });
        }
    }
}
