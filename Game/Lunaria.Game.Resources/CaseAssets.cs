using Lunaria.Game.Resources.Tables;

namespace Lunaria.Game.Resources;

public sealed class CaseAssets
{
    public const uint BeginningCaseId = 1001;

    private readonly Dictionary<uint, PCaseTable> _cases = [];
    private readonly Dictionary<ulong, PCaseClueTable> _clues = [];
    private readonly Dictionary<uint, List<PCaseClueTable>> _cluesByCase = [];
    private readonly Dictionary<ulong, PCaseEvidenceTable> _evidence = [];
    private readonly Dictionary<uint, List<PCaseEvidenceTable>> _evidenceByCase = [];
    private readonly Dictionary<ulong, PCaseStageTable> _stages = [];

    public CaseAssets(
        IReadOnlyDictionary<string, PCaseTable> cases,
        IReadOnlyDictionary<string, PCaseStageTable> stages,
        IReadOnlyDictionary<string, PCaseClueTable> clues,
        IReadOnlyDictionary<string, PCaseEvidenceTable> evidence
    )
    {
        foreach (var row in cases.Values)
        {
            _cases[row.Id] = row;
        }

        foreach (var row in stages.Values)
        {
            _stages[row.Id] = row;
        }

        foreach (var row in clues.Values)
        {
            _clues[row.Id] = row;
        }

        foreach (var row in evidence.Values)
        {
            _evidence[row.Id] = row;
        }

        foreach (var row in _clues.Values)
        {
            (_cluesByCase.TryGetValue(row.CaseId, out var caseClues) ? caseClues : _cluesByCase[row.CaseId] = []).Add(row);
        }

        foreach (var row in _evidence.Values)
        {
            (_evidenceByCase.TryGetValue(row.CaseId, out var evidenceRows) ? evidenceRows : _evidenceByCase[row.CaseId] = []).Add(row);
        }

        if (!_cases.ContainsKey(BeginningCaseId))
            throw new ResourceException("P_CaseTable.json", $"p_casetable has no beginning case {BeginningCaseId}");

        if (_stages.Count == 0)
            throw new ResourceException("P_CaseStageTable.json", "p_casestagetable has no rows");

        if (_clues.Count == 0)
            throw new ResourceException("P_CaseClueTable.json", "p_casecluetable has no rows");

        foreach (var @case in _cases.Values)
        foreach (var stageId in @case.StageIdList)
        {
            if (!_stages.TryGetValue(stageId, out var stage))
                throw new ResourceException(
                    "P_CaseStageTable.json", $"p_casetable {@case.Id} references missing stage {stageId}");

            if (stage.CaseId != @case.Id)
                throw new ResourceException(
                    "P_CaseStageTable.json", $"p_casestagetable {stage.Id} belongs to {stage.CaseId}, not {@case.Id}");

            foreach (var clueId in stage.ClueIdList)
            {
                if (!_clues.TryGetValue(clueId, out var clue) || clue.CaseId != @case.Id)
                    throw new ResourceException(
                        "P_CaseClueTable.json", $"p_casestagetable {stage.Id} references invalid clue {clueId}");
            }
        }

        foreach (var row in _evidence.Values)
        {
            if (!_cases.ContainsKey(row.CaseId))
                throw new ResourceException(
                    "P_CaseEvidenceTable.json", $"p_caseevidencetable {row.Id} references missing case {row.CaseId}");
        }
    }

    public bool CaseExists(uint id) => _cases.ContainsKey(id);
    public bool ClueExists(ulong id) => _clues.ContainsKey(id);
    public bool EvidenceExists(ulong id) => _evidence.ContainsKey(id);
    public PCaseClueTable? Clue(ulong id) => _clues.GetValueOrDefault(id);
    public PCaseEvidenceTable? Evidence(ulong id) => _evidence.GetValueOrDefault(id);
    public IReadOnlyList<ulong> Stages(uint caseId) => _cases.GetValueOrDefault(caseId)?.StageIdList ?? [];
    public IReadOnlyList<ulong> StageClues(ulong stageId) => _stages.GetValueOrDefault(stageId)?.ClueIdList ?? [];
    public IReadOnlyList<PCaseClueTable> Clues(uint caseId) => _cluesByCase.GetValueOrDefault(caseId) ?? [];
    public IReadOnlyList<PCaseEvidenceTable> Evidence(uint caseId) => _evidenceByCase.GetValueOrDefault(caseId) ?? [];
}
