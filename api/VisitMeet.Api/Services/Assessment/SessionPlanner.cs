using VisitMeet.Api.Models;

namespace VisitMeet.Api.Services.Assessment;

public static class SessionPlanner
{
    public static bool IsVisible(GuideTask task, IReadOnlyDictionary<int, SessionTaskAnswer> answers)
    {
        if (task.ConditionTaskNumber is not int parent)
        {
            return true;
        }
        var required = AssessmentMapper.Strings(task.ConditionChoicesJson);
        if (required.Count == 0)
        {
            return true;
        }
        if (!answers.TryGetValue(parent, out var answer))
        {
            return false;
        }
        var selected = AssessmentMapper.Strings(answer.ChoicesJson);
        return required.Any(need => selected.Any(got => string.Equals(got, need, StringComparison.OrdinalIgnoreCase)));
    }

    public static string Coverage(GuideTask task, IReadOnlyDictionary<int, SessionTaskAnswer> answers)
    {
        if (!IsVisible(task, answers))
        {
            return TaskCoverage.NotApplicable;
        }
        if (!answers.TryGetValue(task.Number, out var answer) || string.IsNullOrWhiteSpace(answer.Status))
        {
            return TaskCoverage.Pending;
        }
        return answer.Status is TaskCoverage.Covered or TaskCoverage.NotCovered
            ? answer.Status
            : TaskCoverage.Pending;
    }

    public static bool ConsentGranted(IReadOnlyList<GuideTask> tasks, IReadOnlyDictionary<int, SessionTaskAnswer> answers)
    {
        var intro = tasks.OrderBy(t => t.Number).FirstOrDefault(t =>
            t.Number == 1 || t.Title.Contains("consent", StringComparison.OrdinalIgnoreCase));
        if (intro is null)
        {
            return true;
        }
        if (Coverage(intro, answers) != TaskCoverage.Covered)
        {
            return false;
        }
        if (!answers.TryGetValue(intro.Number, out var answer))
        {
            return false;
        }
        var choices = AssessmentMapper.Strings(answer.ChoicesJson);
        if (choices.Count == 0)
        {
            return true;
        }
        return choices.Any(c => c.Equals("Yes", StringComparison.OrdinalIgnoreCase));
    }
}
