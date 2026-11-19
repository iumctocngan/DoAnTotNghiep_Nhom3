namespace CmsEdu.Domain.Entities;

public class LessonClo
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public int LessonId { get; set; }
    public Guid DocumentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FileName { get; set; } = "";
    public byte[] Workbook { get; set; } = [];
    public string DefinitionJson { get; set; } = "";
}
