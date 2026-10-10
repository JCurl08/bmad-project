# tools

## verify.sh

Runs the Unity project's full verification, one Unity process at a time:

1. `edit`: the EditMode tests, except the `Slow` category;
2. `robust`: the `Slow` EditMode tests (the 500-seed generation check, about 6 minutes);
3. `0`-`8`: the PlayMode tests, in class batches (never all in one process, which runs out of memory);
4. `sweep`: the seed sweep (`CubeSceneBuilder.RunSeedSweep`) and a check that the determinism fixture matches `HEAD`.

```bash
tools/verify.sh mytag            # everything: edit, robust, every batch, sweep
tools/verify.sh mytag edit       # one part: edit, robust, a batch number (0-8) or sweep
tools/verify.sh --list           # the PlayMode batches
```

On a low-memory machine, run one part per call. Close the Unity editor first: Unity refuses a project that another
editor holds. Each part appends a line to `verify_<tag>.txt`, so the summary reads like this:

```
EditMode exit=0 total="..." passed="..." failed="0"
EditMode (Slow: seeds 1-500) exit=0 total="1" passed="1" failed="0"
batch 0 exit=0 total="..." passed="..." failed="0"
...
sweep exit=0: Seed sweep: 50/50 seeds ..., 0 failing.
fixture unchanged
```

The tag comes first and may not be a part name (`tools/verify.sh edit` is refused). The exit status is 0 only when every
part it ran passed: a test part needs Unity to exit 0 and a results file with at least one test and none failed; the
sweep needs 0 failing seeds and an unchanged fixture. For an intended re-record of the fixture, run the sweep part with
`ALLOW_FIXTURE_CHANGE=1`: it then reports `FIXTURE CHANGED (allowed ...)` and passes.

| Variable | Default |
|---|---|
| `UNITY` | The Unity Hub install of the version in `game/ProjectSettings/ProjectVersion.txt` |
| `PROJECT` | `game/`, next to this folder |
| `OUT` | `$PROJECT/Logs/verify` (git ignores it): test results, logs and the summary |
| `ALLOW_FIXTURE_CHANGE` | unset: a changed determinism fixture fails the sweep part; `1` allows it |

When you add a PlayMode test class, add it to a batch in `BATCHES`. The EditMode `VerifyScriptTests` fail when a
PlayMode test class is in no batch, or when a batch names a class that no longer exists.
