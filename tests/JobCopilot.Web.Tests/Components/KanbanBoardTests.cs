using Bunit;
using JobCopilot.Application.Applications;
using JobCopilot.Domain.Applications;
using JobCopilot.Web.Components.Shared;

namespace JobCopilot.Web.Tests.Components;

public class KanbanBoardTests : BunitContext
{
    private static readonly DateOnly Today = new(2026, 11, 14);

    private static ApplicationCard Card(string headline, ApplicationStatus status, DateOnly? followUp = null) => new()
    {
        Id = Guid.NewGuid(),
        JobAdId = Guid.NewGuid(),
        Headline = headline,
        EmployerName = "Bolaget AB",
        Status = status,
        MatchScore = 60,
        FollowUpOn = followUp,
    };

    private IRenderedComponent<KanbanBoard> RenderBoard(IReadOnlyList<ApplicationCard> cards, Action<KanbanBoard.CardMove>? onMove = null) =>
        Render<KanbanBoard>(parameters => parameters
            .Add(p => p.Cards, cards)
            .Add(p => p.Today, Today)
            .Add(p => p.OnMove, move => onMove?.Invoke(move)));

    [Fact]
    public void RendersTheFivePipelineColumnsInOrder()
    {
        var cut = RenderBoard([]);

        Assert.Equal(
            ["Sparad", "Skickad", "Intervju", "Erbjudande", "Avslag"],
            cut.FindAll(".board-column > header > span:first-child").Select(title => title.TextContent));
    }

    [Fact]
    public void PlacesEachCardInItsStatusColumn()
    {
        var cut = RenderBoard(
        [
            Card("A", ApplicationStatus.Saved),
            Card("B", ApplicationStatus.Saved),
            Card("C", ApplicationStatus.Interview),
        ]);

        Assert.Equal(["A", "B"], Headlines(cut, ApplicationStatus.Saved));
        Assert.Equal(["C"], Headlines(cut, ApplicationStatus.Interview));
        Assert.Empty(Headlines(cut, ApplicationStatus.Sent));
        Assert.Equal("2", cut.Find("[data-status=Saved] .board-count").TextContent);
    }

    [Fact]
    public void ChoosingAColumnInTheList_RaisesMove()
    {
        KanbanBoard.CardMove? move = null;
        var card = Card("A", ApplicationStatus.Saved);
        var cut = RenderBoard([card], m => move = m);

        cut.Find("select").Change(ApplicationStatus.Sent.ToString());

        Assert.Equal(new KanbanBoard.CardMove(card.Id, ApplicationStatus.Sent), move);
    }

    [Fact]
    public void DraggingACardToAnotherColumn_RaisesMove()
    {
        KanbanBoard.CardMove? move = null;
        var card = Card("A", ApplicationStatus.Saved);
        var cut = RenderBoard([card], m => move = m);

        cut.Find(".board-card").DragStart();
        cut.Find("[data-status=Interview]").DragEnter();
        Assert.Contains("is-drop-target", cut.Find("[data-status=Interview]").ClassList);

        cut.Find("[data-status=Interview]").Drop();

        Assert.Equal(new KanbanBoard.CardMove(card.Id, ApplicationStatus.Interview), move);
        Assert.DoesNotContain("is-drop-target", cut.Find("[data-status=Interview]").ClassList);
    }

    [Fact]
    public void DroppingACardOnItsOwnColumn_DoesNothing()
    {
        var moves = 0;
        var cut = RenderBoard([Card("A", ApplicationStatus.Saved)], _ => moves++);

        cut.Find(".board-card").DragStart();
        cut.Find("[data-status=Saved]").Drop();

        Assert.Equal(0, moves);
    }

    [Fact]
    public void DropWithoutADrag_DoesNothing()
    {
        var moves = 0;
        var cut = RenderBoard([Card("A", ApplicationStatus.Saved)], _ => moves++);

        cut.Find("[data-status=Sent]").Drop();

        Assert.Equal(0, moves);
    }

    [Fact]
    public void DueFollowUp_IsHighlighted()
    {
        var cut = RenderBoard(
        [
            Card("Försenad", ApplicationStatus.Sent, Today.AddDays(-2)),
            Card("Framtida", ApplicationStatus.Sent, Today.AddDays(3)),
        ]);

        var chips = cut.FindAll(".chip").Where(chip => chip.TextContent.Contains("Följ upp", StringComparison.Ordinal)).ToList();

        Assert.Equal(2, chips.Count);
        Assert.Contains("chip-warn", chips[0].ClassList);
        Assert.Contains("för 2 dagar sedan", chips[0].TextContent);
        Assert.DoesNotContain("chip-warn", chips[1].ClassList);
        Assert.Contains("om 3 dagar", chips[1].TextContent);
    }

    private static List<string> Headlines(IRenderedComponent<KanbanBoard> cut, ApplicationStatus status) =>
        cut.FindAll($"[data-status={status}] .board-card h3").Select(heading => heading.TextContent).ToList();
}
