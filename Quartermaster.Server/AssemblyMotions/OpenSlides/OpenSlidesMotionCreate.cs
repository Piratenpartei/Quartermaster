namespace Quartermaster.Server.AssemblyMotions.OpenSlides;

public record OpenSlidesMotionCreate(int MeetingId, string Title, string Number, string TextHtml, string? ReasonHtml, int CategoryId, string AdditionalSubmitter);

public record OpenSlidesMotionUpdate(int MotionId, string Title, string TextHtml, string? ReasonHtml, string AdditionalSubmitter);
