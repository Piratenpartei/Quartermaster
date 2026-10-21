using System;
using System.Collections.Generic;
using Quartermaster.Api;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>Who may work with the UU-Assistent: holders of the permission in a campaign's chapter or above; the shared office address book needs it in any chapter.</summary>
public class SignatureCampaignAccess {
    private readonly PermissionContext _perms;

    public SignatureCampaignAccess(PermissionContext perms) {
        _perms = perms;
    }

    public bool CanUseOffices() {
        return _perms.HasInAnyChapter(PermissionIdentifier.ManageSupportSignatures);
    }

    public bool CanManage(SignatureCampaign campaign) {
        return CanManageChapter(campaign.ChapterId);
    }

    public bool CanManageChapter(Guid chapterId) {
        return _perms.Has(chapterId, PermissionIdentifier.ManageSupportSignatures);
    }

    /// <summary>Chapters whose campaigns the caller manages, including sub-chapters; null for all.</summary>
    public List<Guid>? ManageableChapterIds() {
        return _perms.GetPermittedChapterIds(PermissionIdentifier.ManageSupportSignatures);
    }
}
