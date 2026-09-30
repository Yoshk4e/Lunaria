using Lunaria.Game.Resources;
using Msg;

namespace Lunaria.Game.Player.Managers;

public sealed record CaseProcessingState(
    uint CaseId,
    uint FinishedPhase,
    IReadOnlySet<ulong> OnSlotClues,
    IReadOnlySet<ulong> DecryptedEvidence
)
{
    public IReadOnlySet<ulong> OwnedClues { get; init; } = new SortedSet<ulong>();
    public IReadOnlySet<ulong> OwnedEvidence { get; init; } = new SortedSet<ulong>();
}

public sealed class CaseManager(GameData assets)
{
    private readonly SortedSet<uint> _finished = [];
    private readonly SortedDictionary<uint, CaseProcessingState> _processing = [];

    public bool IsDirty { get; private set; }

    public IReadOnlyDictionary<uint, CaseProcessingState> Processing => _processing;

    public IReadOnlyCollection<uint> Finished => _finished;

    public void Load(
        IEnumerable<(uint CaseId, uint FinishedPhase, IEnumerable<ulong> OnSlotClues, IEnumerable<ulong> DecryptedEvidence)> persisted,
        IEnumerable<uint> finished
    )
    {
        _processing.Clear();
        _finished.Clear();

        foreach (var row in persisted)
        {
            if (!assets.Cases.CaseExists(row.CaseId) || _finished.Contains(row.CaseId))
                continue;

            var clues = new SortedSet<ulong>(
                row.OnSlotClues.Where(clue => BelongsToCase(clue, row.CaseId)));

            if (AllStagesComplete(row.CaseId, clues))
            {
                _processing.Remove(row.CaseId);
                _finished.Add(row.CaseId);
                continue;
            }

            var evidence = new SortedSet<ulong>(
                row.DecryptedEvidence.Where(item => assets.Cases.Evidence(item)?.CaseId == row.CaseId));

            _processing[row.CaseId] = new CaseProcessingState(
                    row.CaseId, ComputeFinishedPhase(row.CaseId, clues), clues, evidence)
                { OwnedClues = clues, OwnedEvidence = evidence };
        }

        foreach (var caseId in finished)
        {
            if (!assets.Cases.CaseExists(caseId))
                continue;

            if (_processing.ContainsKey(caseId))
                continue;

            _finished.Add(caseId);
        }

        IsDirty = false;
    }

    public uint EnsureStarted()
    {
        if (_processing.Count > 0 || _finished.Count > 0)
            return 0;

        const uint beginning = CaseAssets.BeginningCaseId;

        if (!assets.Cases.CaseExists(beginning))
            return 0;

        _processing[beginning] = new CaseProcessingState(beginning, FinishedPhase: 0, new SortedSet<ulong>(), new SortedSet<ulong>());
        Dirty();
        return beginning;
    }

    public bool IsProcessing(uint caseId) => _processing.ContainsKey(caseId);

    public void ClearDirty() => IsDirty = false;

    public void LoadOwned(IEnumerable<(uint CaseId, IEnumerable<ulong> Clues, IEnumerable<ulong> Evidence)> owned)
    {
        foreach (var row in owned)
        {
            if (!_processing.TryGetValue(row.CaseId, out var state)) continue;

            _processing[row.CaseId] = state with {
                OwnedClues = new SortedSet<ulong>(row.Clues.Where(id => BelongsToCase(id, row.CaseId)).Concat(state.OnSlotClues)),
                OwnedEvidence = new SortedSet<ulong>(row.Evidence.Where(id => assets.Cases.Evidence(id)?.CaseId == row.CaseId)
                    .Concat(state.DecryptedEvidence))
            };
        }
    }

    public bool GiveClue(ulong clueId)
    {
        if (assets.Cases.Clue(clueId) is not {} clue || !_processing.TryGetValue(clue.CaseId, out var state)) return false;
        if (state.OwnedClues.Contains(clueId)) return true;

        _processing[clue.CaseId] = state with { OwnedClues = new SortedSet<ulong>(state.OwnedClues) { clueId } };
        Dirty();
        return true;
    }

    public bool GiveEvidence(ulong evidenceId)
    {
        if (assets.Cases.Evidence(evidenceId) is not {} evidence || !_processing.TryGetValue(evidence.CaseId, out var state)) return false;
        if (state.OwnedEvidence.Contains(evidenceId)) return true;

        _processing[evidence.CaseId] = state with { OwnedEvidence = new SortedSet<ulong>(state.OwnedEvidence) { evidenceId } };
        Dirty();
        return true;
    }

    /// <summary>Only list granted leads to avoid revealing the whole investigation.</summary>
    public SCCaseData ToCaseData()
    {
        var data = new SCCaseData();

        foreach (var state in _processing.Values)
        {
            var processing = new ProcessingCase {
                CaseId = state.CaseId,
                FinishedPhase = state.FinishedPhase
            };

            foreach (var clueId in state.OwnedClues)
            {
                processing.ClueStatus.Add(new ClueStatus {
                    ClueId = clueId,
                    OnSlot = state.OnSlotClues.Contains(clueId)
                });
            }

            foreach (var evidenceId in state.OwnedEvidence)
            {
                processing.EvidenceStatus.Add(new EvidenceStatus {
                    EvidenceId = evidenceId,
                    Decrypted = state.DecryptedEvidence.Contains(evidenceId)
                });
            }
            data.ProcessingCase.Add(processing);
        }
        data.FinishedCase.AddRange(_finished.Select(caseId => (ulong)caseId));
        return data;
    }

    public (int Result, uint CaseId, uint FinishedPhase) PutClue(ulong clueId)
    {
        if (!assets.Cases.ClueExists(clueId))
            return ((int)EnmTextCode.EnmTextWrongParam, 0, 0);

        var clue = assets.Cases.Clue(clueId)!;

        if (_finished.Contains(clue.CaseId))
            return (0, clue.CaseId, (uint)assets.Cases.Stages(clue.CaseId).Count);

        if (!_processing.TryGetValue(clue.CaseId, out var state) || !state.OwnedClues.Contains(clueId))
            return ((int)EnmTextCode.EnmTextWrongParam, 0, 0);

        if (state.OnSlotClues.Contains(clueId))
            return (0, clue.CaseId, state.FinishedPhase);

        var clues = new SortedSet<ulong>(state.OnSlotClues) { clueId };
        var phase = ComputeFinishedPhase(clue.CaseId, clues);

        if (AllStagesComplete(clue.CaseId, clues))
        {
            _processing.Remove(clue.CaseId);
            _finished.Add(clue.CaseId);
        } else
        {
            _processing[clue.CaseId] = state with { OnSlotClues = clues, FinishedPhase = phase };
        }
        Dirty();

        return (0, clue.CaseId, phase);
    }

    public int DecryptEvidence(ulong evidenceId)
    {
        if (!assets.Cases.EvidenceExists(evidenceId))
            return (int)EnmTextCode.EnmTextWrongParam;

        var evidence = assets.Cases.Evidence(evidenceId)!;

        if (!_processing.TryGetValue(evidence.CaseId, out var state) || !state.OwnedEvidence.Contains(evidenceId))
            return (int)EnmTextCode.EnmTextWrongParam;

        if (state.DecryptedEvidence.Contains(evidenceId))
            return 0;

        var decrypted = new SortedSet<ulong>(state.DecryptedEvidence) { evidenceId };
        _processing[evidence.CaseId] = state with { DecryptedEvidence = decrypted };
        Dirty();
        return 0;
    }

    public (bool Opened, ulong[] ClueIds, ulong[] EvidenceIds)? OpenCase(uint caseId)
    {
        if (!assets.Cases.CaseExists(caseId) || _processing.ContainsKey(caseId) || _finished.Contains(caseId))
            return null;

        _processing[caseId] = new CaseProcessingState(caseId, FinishedPhase: 0, new SortedSet<ulong>(), new SortedSet<ulong>());
        Dirty();

        return (true, [], []);
    }

    public static SCCaseReceiveNtf ToReceiveNotification(uint caseId, ulong[] clueIds, ulong[] evidenceIds)
    {
        var ntf = new SCCaseReceiveNtf { CaseId = caseId };
        ntf.ClueIds.AddRange(clueIds);
        ntf.EvidenceIds.AddRange(evidenceIds);
        return ntf;
    }

    public ulong NextStage(uint caseId, uint finishedPhase)
    {
        var stages = assets.Cases.Stages(caseId);
        return finishedPhase < stages.Count ? stages[(int)finishedPhase] : 0;
    }

    private bool AllStagesComplete(uint caseId, IReadOnlySet<ulong> onSlot)
    {
        foreach (var stageId in assets.Cases.Stages(caseId))
        {
            if (!StageComplete(stageId, onSlot))
                return false;
        }
        return true;
    }

    private bool StageComplete(ulong stageId, IReadOnlySet<ulong> onSlot)
    {
        foreach (var clueId in assets.Cases.StageClues(stageId))
        {
            if (!onSlot.Contains(clueId))
                return false;
        }
        return true;
    }

    private uint ComputeFinishedPhase(uint caseId, IReadOnlySet<ulong> onSlot)
    {
        uint phase = 0;

        foreach (var stageId in assets.Cases.Stages(caseId))
        {
            if (!StageComplete(stageId, onSlot))
                break;

            phase++;
        }
        return phase;
    }

    private bool BelongsToCase(ulong clueId, uint caseId) => assets.Cases.Clue(clueId)?.CaseId == caseId;

    private void Dirty() => IsDirty = true;
}
