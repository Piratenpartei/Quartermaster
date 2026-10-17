using System.Collections.Generic;
using System.Linq;
using Quartermaster.Server.AssemblyAssistant.OpenSlides;

namespace Quartermaster.Server.AssemblyAssistant.Templates;

/// <summary>
/// Procedural polls cannot hang on a motion's agenda item, so every motion gets a plain topic "&lt;number&gt;: Verfahren" as its
/// child. It is found again by title, so nothing needs to be stored.
/// </summary>
public static class ProceduralTopics {
    public static string TitleFor(OpenSlidesAgendaItemView motionItem) {
        return $"{motionItem.MotionNumber ?? motionItem.Title}: Verfahren";
    }

    public static string? Find(OpenSlidesMeetingView view, OpenSlidesAgendaItemView motionItem) {
        var title = TitleFor(motionItem);
        return view.AgendaItems
            .FirstOrDefault(a => a.ParentId == motionItem.Id && a.ContentObjectId.StartsWith("topic/") && a.Title == title)?
            .ContentObjectId;
    }

    public static List<OpenSlidesAgendaItemView> MotionsWithout(OpenSlidesMeetingView view) {
        return view.AgendaItems
            .Where(a => a.ContentObjectId.StartsWith("motion/") && Find(view, a) == null)
            .ToList();
    }
}
