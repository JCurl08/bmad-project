#!/usr/bin/env bash
# Full verification of the Unity project, one Unity process at a time (the PlayMode tests are run in class batches, never
# all in one process): EditMode, every PlayMode batch, then the seed sweep and the determinism fixture check.
#
# Usage: tools/verify.sh [tag] [part ...]
#   tag   names the results (default: v); a summary goes to $OUT/verify_<tag>.txt
#   part  edit (EditMode but the Slow category) | robust (the Slow category: seeds 1-500) | 0 .. 7 (a PlayMode batch) |
#         sweep | all (default: all). Run one part per call on a low-memory machine. The tag may not be a part name.
#   tools/verify.sh --list   prints the PlayMode batches, one per line.
#
# Environment (all optional):
#   UNITY    the Unity editor executable (default: the Unity Hub install of the version in ProjectSettings/ProjectVersion.txt)
#   PROJECT  the Unity project (default: game/ next to this script's folder)
#   OUT      where results, logs and the summary go (default: $PROJECT/Logs/verify, ignored by git)
#   ALLOW_FIXTURE_CHANGE=1  an intended determinism re-record: the sweep part reports the changed fixture but passes
#
# The editor must be closed (Unity refuses a project another editor holds). Exit status: 0 when every part run passed (a
# test part needs results with at least one test and none failed; the sweep needs 0 failing and an unchanged fixture).

set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/.." && pwd)"
PROJECT="${PROJECT:-$repo/game}"
OUT="${OUT:-$PROJECT/Logs/verify}"

# Every PlayMode test class, in batches small enough for one process each. The EditMode VerifyScriptTests fail when a
# PlayMode test class is in no batch (or a batch names a class that is gone): add new classes here.
P="Game.Cube.Tests."
BATCHES=(
"${P}CoreSceneTests;${P}CubeMapSceneTests"
"${P}EarthComponentTests;${P}EarthSceneTests;${P}IceSceneTests;${P}WindSceneTests;${P}MathComponentTests;${P}MathSceneTests;${P}IslandPuzzleSceneTests"
"${P}BiologyComponentTests;${P}BiologySceneTests;${P}ChemistryComponentTests;${P}ChemistrySceneTests;${P}FaceRepairSceneTests;${P}RewardSceneTests;${P}LockSceneTests;${P}QuestSceneTests"
"${P}PhysicsComponentTests;${P}PhysicsSceneTests;${P}CubeSceneTests;${P}ItemComponentTests;${P}ItemSceneTests"
"${P}CombatComponentTests;${P}CombatSceneTests;${P}NpcComponentTests;${P}NpcSceneTests;${P}FeedbackTests"
"${P}TownSceneTests;${P}TownInteriorSceneTests;${P}LairSceneTests;${P}RunLoopSceneTests;Game.Tracer.Tests.ProbeTests;Game.Tracer.Tests.TracerSceneTests"
"${P}IslandWalkTests;${P}IslandTrailWalkTests"
"${P}TempleSceneTests"
)

if [ "${1:-}" = "--list" ]; then
  printf '%s\n' "${BATCHES[@]}"
  exit 0
fi

# A path Unity understands (a Windows path under Git Bash or MSYS, unchanged elsewhere).
native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

if [ -z "${UNITY:-}" ]; then
  version="$(sed -n 's/^m_EditorVersion: *//p' "$PROJECT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
  case "$(uname -s)" in
    Darwin) UNITY="/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity" ;;
    Linux) UNITY="$HOME/Unity/Hub/Editor/$version/Editor/Unity" ;;
    *) pf="${PROGRAMFILES:-C:/Program Files}"
       command -v cygpath >/dev/null 2>&1 && pf="$(cygpath -u "$pf")"
       UNITY="$pf/Unity/Hub/Editor/$version/Editor/Unity.exe" ;;
  esac
fi
if [ ! -x "$UNITY" ] && [ ! -f "$UNITY" ]; then
  echo "verify: no Unity editor at '$UNITY' (set UNITY)" >&2
  exit 2
fi

tag="${1:-v}"
[ $# -gt 0 ] && shift
case "$tag" in
  edit|robust|sweep|all|[0-9]|[0-9][0-9])
    echo "verify: the tag '$tag' is a part name; give a tag first (tools/verify.sh <tag> [part ...])" >&2
    exit 2 ;;
esac
parts=("$@")
[ ${#parts[@]} -eq 0 ] && parts=(all)
if [ "${parts[0]}" = "all" ]; then
  parts=(edit robust)
  for i in "${!BATCHES[@]}"; do parts+=("$i"); done
  parts+=(sweep)
fi

mkdir -p "$OUT"
summary="$OUT/verify_$tag.txt"
project="$(native "$PROJECT")"
out="$(native "$OUT")"
status=0

counts() { grep -o 'total="[0-9]*" passed="[0-9]*" failed="[0-9]*"' "$1" 2>/dev/null | head -1; }

# A test part passes only when Unity exited 0 and its results exist with at least one test and none failed.
results_ok() {
  local c total failed
  c="$(counts "$1")"
  [ -n "$c" ] || return 1
  total="$(printf '%s' "$c" | sed -n 's/.*total="\([0-9]*\)".*/\1/p')"
  failed="$(printf '%s' "$c" | sed -n 's/.*failed="\([0-9]*\)".*/\1/p')"
  [ "${total:-0}" -gt 0 ] && [ "${failed:-1}" -eq 0 ]
}

# Runs the EditMode tests of a category filter into <name>.xml/.log and records the line.
editmode() {
  local name="$1" label="$2" filter="$3"
  rm -f "$OUT/${tag}_$name.xml"
  "$UNITY" -batchmode -nographics -projectPath "$project" -runTests -testPlatform EditMode -testCategory "$filter" \
    -testResults "$out/${tag}_$name.xml" -logFile "$out/${tag}_$name.log"
  code=$?
  local c; c="$(counts "$OUT/${tag}_$name.xml")"
  echo "$label exit=$code ${c:-no results}" >> "$summary"
  results_ok "$OUT/${tag}_$name.xml" || code=1
}

for part in "${parts[@]}"; do
  [ "$part" = "edit" ] && [ "$part" = "${parts[0]}" ] && : > "$summary"
  touch "$summary"
  before=$(wc -l < "$summary")
  case "$part" in
    edit) editmode edit EditMode '!Slow' ;;
    robust) editmode robust "EditMode (Slow: seeds 1-500)" 'Slow' ;;
    sweep)
      "$UNITY" -batchmode -nographics -projectPath "$project" -quit -executeMethod CubeSceneBuilder.RunSeedSweep \
        -logFile "$out/${tag}_sweep.log"
      code=$?
      line="$(grep -h 'Seed sweep:' "$OUT/${tag}_sweep.log" | tail -1)"
      echo "sweep exit=$code: $line" >> "$summary"
      case "$line" in *", 0 failing."*) ;; *) code=1 ;; esac
      if git -C "$repo" diff --quiet HEAD -- game/Assets/Tests/EditMode/DeterminismSignatures.txt; then
        echo "fixture unchanged" >> "$summary"
      elif [ "${ALLOW_FIXTURE_CHANGE:-0}" = "1" ]; then
        echo "FIXTURE CHANGED (allowed: ALLOW_FIXTURE_CHANGE=1)" >> "$summary"
      else
        echo "FIXTURE CHANGED" >> "$summary"
        code=1
      fi
      ;;
    *)
      if ! [[ "$part" =~ ^[0-9]+$ ]] || [ "$part" -ge "${#BATCHES[@]}" ]; then
        echo "verify: unknown part '$part' (edit, robust, 0-$(( ${#BATCHES[@]} - 1 )), sweep or all)" >&2
        exit 2
      fi
      rm -f "$OUT/${tag}_pm$part.xml"
      "$UNITY" -batchmode -nographics -projectPath "$project" -runTests -testPlatform PlayMode -testFilter "${BATCHES[$part]}" \
        -testResults "$out/${tag}_pm$part.xml" -logFile "$out/${tag}_pm$part.log"
      code=$?
      c="$(counts "$OUT/${tag}_pm$part.xml")"
      echo "batch $part exit=$code ${c:-no results}" >> "$summary"
      grep -h "Skill check" "$OUT/${tag}_pm$part.log" >> "$summary"
      results_ok "$OUT/${tag}_pm$part.xml" || code=1
      ;;
  esac
  [ "$code" -ne 0 ] && status=1
  tail -n +$((before + 1)) "$summary"
done

exit $status
