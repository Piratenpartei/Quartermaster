using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;

namespace Quartermaster.Server.NewsPosts.Endpoints;

/// <summary>Builds item DTOs with candidates and their cited passages; needs three repositories, so it is a mapper rather than a method on the entity.</summary>
public static class NewsItemMapper {
    public static List<NewsItemDTO> ToDtos(List<NewsItem> items, NewsItemRepository itemRepo, ProgrammeRepository programmeRepo, NewsFeedRepository feedRepo) {
        var feeds = feedRepo.GetAll().ToDictionary(f => f.Id, f => f.Name);
        var candidates = itemRepo.GetCandidatesForItems(items.Select(i => i.Id).ToList());
        var sections = programmeRepo.GetSectionsByIds(candidates.Select(c => c.SectionId).Distinct().ToList()).ToDictionary(s => s.Id);

        return items
            .Select(item => new NewsItemDTO {
                Id = item.Id,
                FeedName = feeds.GetValueOrDefault(item.FeedId, ""),
                Title = item.Title,
                Url = item.Url,
                Teaser = item.Teaser,
                HasFullText = item.HasFullText,
                PublishedAt = new DateTimeOffset(DateTime.SpecifyKind(item.PublishedAt, DateTimeKind.Utc)),
                Status = (NewsItemStatusDTO)(int)item.Status,
                TriageReason = item.TriageReason,
                LastError = item.LastError,
                Candidates = candidates
                    .Where(c => c.NewsItemId == item.Id)
                    .Select(c => ToCandidateDto(c, sections.GetValueOrDefault(c.SectionId)))
                    .ToList()
            })
            .ToList();
    }

    private static PostCandidateDTO ToCandidateDto(PostCandidate candidate, ProgrammeSection? section) {
        return new PostCandidateDTO {
            Id = candidate.Id,
            Text = candidate.Text,
            SectionHeadingPath = section?.HeadingPath ?? "",
            SectionBody = section?.Body ?? "",
            SectionUrl = section == null
                ? ""
                : $"https://wiki.piratenpartei.de/{Uri.EscapeDataString(section.PageTitle.Replace(' ', '_')).Replace("%2F", "/")}#{Uri.EscapeDataString(section.Anchor)}"
        };
    }
}
