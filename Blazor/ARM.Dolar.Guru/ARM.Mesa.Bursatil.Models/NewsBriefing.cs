namespace ARM.Mesa.Bursatil.Models;

public sealed class NewsBriefing
{
    public long Id { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime CoverageStartUtc { get; set; }
    public DateTime CoverageEndUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public string Status { get; set; } = "draft";
    public string Model { get; set; } = "";
    public string Provider { get; set; } = "";
    public string? ReviewedBy { get; set; }
    public List<BriefingStatement> Summary { get; set; } = [];
    public List<BriefingTopic> Topics { get; set; } = [];
    public List<BriefingSource> Sources { get; set; } = [];
}

public sealed class BriefingStatement
{
    public string Text { get; set; } = "";
    public List<BriefingCitation> Citations { get; set; } = [];
}

public sealed class BriefingCitation
{
    public int NewsId { get; set; }
    public string Evidence { get; set; } = "";
}

public sealed class BriefingTopic
{
    public string Title { get; set; } = "";
    public bool IsInternational { get; set; }
    public BriefingStatement WhatHappened { get; set; } = new();
    public BriefingStatement WhyItMatters { get; set; } = new();
    public BriefingStatement WhatToWatch { get; set; } = new();
}

// Frozen with the edition: later feed corrections must not change the evidence reviewed.
public sealed class BriefingSource
{
    public int NewsId { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string PublisherKey { get; set; } = "";
    public string EvidenceText { get; set; } = "";
    public DateTime PublishedAtUtc { get; set; }
    public bool IsInternational { get; set; }
}
