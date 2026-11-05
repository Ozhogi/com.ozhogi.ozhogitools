# Seiki — API reference for LLM coding agents

Machine-oriented reference for the `com.ozhogi.seiki` package. Contains only what
an LLM programmer needs to call or modify the gaze API correctly.

## Namespace, assemblies, public surface

- Runtime code: `Runtime/`, namespace `Ozhogi.Seiki`, assembly `Ozhogi.Seiki.Runtime`
  (`Ozhogi.Seiki.Runtime.asmdef`).
- Editor code: `Editor/`, assembly `Ozhogi.Seiki.Editor`.
- Public API = three files: `Runtime/Seiki.cs`, `Runtime/SeikiLookController.cs`,
  `Runtime/SeikiProfile.cs`.
- `Runtime/Internal/` is implementation detail: caller code must not use it, and no
  public API may be added there.
- Package version lives in `package.json` (currently 1.2.0); bump it on public API
  changes.

## Component split (who does what)

| Component | Responsibility |
|---|---|
| `Seiki` | Mechanics ("how to look"): saccades, blinking, eyelids, head/neck rotation, SCM neck shapes. |
| `SeikiLookController` | High-level attention ("what to look at"): default idle target + distraction to points of interest. |
| NPC Brain (external game code) | Attention policy: moves `SeikiLookController.defaultIdleTarget`; may also call `Seiki` target commands directly. |

## `Seiki : MonoBehaviour`

### Target commands

| Method | Behavior |
|---|---|
| `void LookAtFace(Transform eyeCenterXform, float headLatency = 0.03f)` | Look at a face: center of the target's eyes. |
| `void LookAtFace(Transform leftEyeXform, Transform rightEyeXform, Transform eyesCenterXform, float headLatency = 0.03f)` | Look at a face; gaze auto-wanders the "social triangle" (left eye ↔ right eye ↔ mouth). |
| `void LookAtSpecificThing(Transform poi, float headLatency = 0.03f)` | Look at an exact target. |
| `void LookAtSpecificThing(Vector3 point, float headLatency = 0.03f)` | Same, position form. Reuses one of two hidden `HideAndDontSave` transforms (round-robin). |
| `void LookAtAreaAround(Transform poi, float headLatency = 0.03f)` | Look "roughly there": target plus macro-saccades around it. |
| `void LookAtAreaAround(Vector3 point, float headLatency = 0.03f)` | Same, position form. |
| `void LookAroundIdly(Transform transformToAvoidLookingAt = null, float headLatency = 0.03f)` | Random gaze wandering; avoids the given transform. Picks a new point when the current one leaves ±45° horizontal / ±30° vertical of head forward. |
| `void ClearLookTarget()` | Smooth reset to straight ahead (~3 s: 2 s hold, 1 s ease), then `StraightAhead` rest state. |

### Other methods

| Method | Purpose |
|---|---|
| `void Blink(bool isShortBlink = true)` | Force a blink. |
| `bool IsSwitchingHeadTarget()` | Head is currently switching targets. |
| `void StartHeadMovement()` | Start head movement manually. |
| `void Initialize()` / `void Update2(float deltaTime)` | Low level; callers normally never call these. |

### `headLatency` semantics (per call)

- `> 0`: eyes move first, head follows after `latency` seconds
  (default `kDefaultHeadLatency = 0.03f` — visually simultaneous).
- `< 0`: head moves first, eyes follow after `|latency|`.
- `= 0`: eyes and head start simultaneously.

### Events (subscribe in `OnEnable`, unsubscribe in `OnDisable`)

| Event | Meaning |
|---|---|
| `event Action OnUpdate2Finished` | Fired at the end of the per-frame eye phase (eyes, blink, SCM applied). Use for logic that reads the final eye state of this frame. `SeikiLookController` runs its logic here. |
| `event Action OnTargetDestroyed` | Current gaze target was destroyed. |
| `event Action OnCannotGetTargetIntoView` | Target is outside the physically reachable cone (head angle from spine base + eye range). The actor keeps straining toward it; the Brain decides what to do. |

### Properties

| Property | Description |
|---|---|
| `Ray LeftEyeRay` / `Ray RightEyeRay` / `Ray EyesCombinedRay` | Eye rays, refreshed every frame. |
| `float eyeDistance` / `float eyeDistanceScale` | Inter-eye distance (m) and its scale relative to 0.064 m. |
| `BlendshapeBank blendshapes` | Advanced: shared eyelid + SCM shape driver. |

### State queries (angular checks only — **no raycasts / line-of-sight**)

| Query | Meaning |
|---|---|
| `bool CanGetIntoView(Vector3 point)` | Head + eyes can physically reach the point (cone from head base, which follows the spine). |
| `bool IsInView(Vector3 target)` | Point is currently inside the view cone of at least one eye. |
| `bool IsGazeAt(Vector3 point, float maxAngle = 15f)` | Combined gaze points at the point. |
| `bool CanChangePointOfAttention()` | Current saccade finished + 0.3 s dead time; safe to redirect attention. |
| `bool IsLookingAtFace()` | Current look target is a face. |
| `Vector3 GetOwnEyeCenter()` | Eye-root position. |
| `Vector3 GetOwnLookDirection()` | Slerped direction of both eyes. |
| `Vector3 GetCurrentHeadTargetPos()` | Position of the current head target. |
| `Vector3 GetHeadDirection()` / `Vector3 GetSpineForward()` / `Transform GetHeadParentXform()` | Head/spine orientation and head base transform. |
| `float GetStareAngleTargetAtMe(Transform targetXform)` | Angle between `target.forward` and (my eye center − target position): how much the target is looking at me. |
| `bool AreInSimilarLookDirection(Vector3 a, Vector3 b)` | Directions from the eyes to `a` and `b` differ by < 15°. |

### Serialized inspector fields

- Bones: `Transform head`, `Transform neck`, `Transform spine`.
- Eyes: `Transform leftEye`, `Transform rightEye`.
- `SkinnedMeshRenderer[] blendshapeRenderers` — all renderers carrying eyelid/neck
  shapes; any renderer that has a shape is driven.
- `SeikiProfile profile`.
- ASG markers: `Transform leftTop`, `leftBottom`, `rightTop`, `rightBottom`.
- Behavior tunables:
  - `float eyeOnlyAngle` `[Range(0, 90)]`, default 18 — gaze shifts smaller than this
    angle rotate the eyes only (head stays).
  - `float gazeShiftBlinkProbability` `[Range(0, 1)]`, default 0.5 — probability of a
    full blink when the gaze shifts to a new target.

`[HideInInspector]` fields (`profileMaxUp`, `profileMaxDown`, `profileMaxLeft`,
`profileMaxRight`, `forwardInHeadSpace`, cached quaternions/positions) are an
editor-managed calibration cache written by `SeikiEditor`. Treat as read-only.

### Notable public constants

- `kDefaultHeadLatency = 0.03f`.
- `kGazeShiftBlinkMinDuration = 0.15f`, `kGazeShiftBlinkMaxDuration = 0.25f` —
  gaze-shift blink length range (content, not a per-character knob).
- `kMaxHorizViewAngle = 100`, `kMaxVertViewAngle = 60` — view cone limits.
- `kBlinkTravelThreshold = 60f` (degrees of accumulated eye travel) and
  `kBlinkSafetyInterval = 10f` (seconds) — natural blink triggers.
- `kAttentionChangeDeadTimeAfterSaccadeEnd = 0.3f`.
- Rotation weights: `mainWeight = 1`, `eyesWeight = 1`, `eyelidsWeight = 1`,
  `headWeight = 0.75`, `bodyWeight = 0.1`, `neckHorizWeight = 0.5`,
  `neckVertWeight = 0.5`.
- `useMicroSaccades` is `static readonly` (`true`), not `const` — `const true`
  constant-folds a guard and triggers CS0162 (unreachable code).
- Saccade pacing: `macroSaccadesPerMinute = 10`, `microSaccadesPerMinuteLookingIdle = 45`,
  `microSaccadesPerMinuteLookingAtPOI = 80`, `saccadeSpeed = 0.5`.
- Gaze-shift blink envelope: `kGazeShiftBlinkCloseFraction = 0.35`,
  `kGazeShiftBlinkHoldFraction = 0.10`, `kGazeShiftBlinkTailAfterSaccade = 0.05`.

### Per-frame execution order (do not break)

| Phase | Hook | Work |
|---|---|---|
| 1 | `EarlyUpdateCallback` (script order **−999999**, added by `Seiki` at runtime) | `controlData.RestoreDefault()` — reset eyelid shapes to authored defaults before all other scripts. |
| 2 | `Update()` | `CheckLatencies()` (applies pending eye/head target starts); head reset if `resetHeadAtFrameStart` (default false). |
| 3 | `LateUpdate()` → `Update1(dt)` | Head orientation (after animation). |
| 4 | `VeryLateUpdateCallback` (script order **15000**) → `Update2(dt)` | Eyes, micro/macro saccades, blinking, SCM shapes; fires `OnUpdate2Finished` last. |

Implications:

- Read final eye rays/positions only in phase 4 or via `OnUpdate2Finished`.
- Eyelid and SCM blend shapes are **owned by Seiki**; external code must not write
  the same shapes (phase 4 overwrites them every frame).
- `StraightAhead` is a live rest pose, not a freeze: eyelid shapes, blinking and SCM
  keep updating there.

## `SeikiLookController : MonoBehaviour`

Same GameObject as `Seiki` (without it: logs a warning, no-op).

### Serialized fields

| Field | Semantics |
|---|---|
| `Transform defaultIdleTarget` | Persistent attention intention. The component **never clears it**; if it leaves the view cone the actor keeps straining toward it (`Seiki.OnCannotGetTargetIntoView` fires). `null` = browse POIs / random directions. The Brain moves this field. |
| `List<Transform> pointsOfInterest` | Distraction candidates. Only active and physically reachable POIs (`Seiki.CanGetIntoView`) are picked; the last picked POI is excluded while another candidate exists (inhibition of return). |
| `float focus` `[Range(0, 1)]`, default 0.5 | Attention commitment. Scales the attention period `Lerp(minLookTime, maxLookTime, focus)` and the head-commit delay when distracted: `Lerp(3.0, 0.8, focus)` seconds for POIs within 30° of forward; farther targets skip the dwell gate and use `kDefaultHeadLatency`. |
| `float minLookTime = 2f` / `float maxLookTime = 8f` | Attention period bounds. |

### Read-only state

- `bool IsLookingAtDefaultTarget` — `defaultIdleTarget != null` and currently on it.
- `bool IsLookingIdly` — currently wandering.
- `void Blink()` — forwards to `Seiki.Blink()`.

### Behavior contract

- Attention period elapses → distraction roll: distract when `Random.value > focus`;
  otherwise stay on the default target.
- Distraction → `Seiki.LookAtSpecificThing(poi, headLatency)`; return to home →
  `Seiki.LookAtSpecificThing(defaultIdleTarget)` with **no** view-cone gate (strains
  toward the home even if it left the cone).
- Browsing (no home): pick a reachable POI → `Seiki.LookAtAreaAround(poi)`, else
  `Seiki.LookAroundIdly()`.
- Runs its logic on `Seiki.OnUpdate2Finished`; on `OnTargetDestroyed` zeroes its
  attention timer to re-evaluate the same frame.

## `SeikiProfile : ScriptableObject`

`[CreateAssetMenu(fileName = "SeikiProfile", menuName = "Seiki", order = 0)]`.

### Eye states

`EyeState eyesClosed, lookingUp, lookingDown, lookingLeft, lookingRight`; each holds
`Vector3 leftEyeEuler`, `Vector3 rightEyeEuler`, `Vector4 asgOffsets`
(component order: R.Top, R.Bottom, L.Top, L.Bottom).

### Neck shape drivers (SCM)

- `List<NeckShapeDriver> neckShapeDrivers`.
- `NeckShapeDriver { string shapeName; List<NeckShapeContribution> contributions }`.
- `NeckShapeContribution { ScmBoneAxis boneAxis = HeadY; float fullAngle = 25f }`.
- Shape weight = `Clamp01(average over contributions of (boneAngle / fullAngle))`.
  One contribution for single-joint corrections (bends); two for head + neck (twists).
  `fullAngle` is signed: negative = opposite direction.
- Angle sources are **local** euler angles (head relative to neck, neck relative to
  spine), normalized with `Utils.NormalizedDegAngle`.

### `enum ScmBoneAxis`

```csharp
HeadX = 0, HeadY = 1, HeadZ = 2, NeckX = 3, NeckY = 4, NeckZ = 5
```

**Explicit int values are a contract** (`ScmComponent` indexes a per-frame angle
cache by `(int)ScmBoneAxis`). Never reorder or renumber.

### Built-in fallback shapes

Used when `neckShapeDrivers` is empty or `profile == null`:

| Shape | Head contribution | Neck contribution |
|---|---|---|
| `Neck Twist Right` | Head Y +25 | Neck Y +25 |
| `Neck Twist Left` | Head Y −25 | Neck Y −25 |
| `Neck Bend Back` | Head X −30 | Neck X −20 |
| `Neck Bend Front` | Head X +20 | Neck X +20 |

Sign convention: positive yaw = turn right, negative pitch = look up. If shapes
mirror in-engine, fix `fullAngle` signs in the profile, not the code.

## Invariants (breaking any of these corrupts persistence or behavior)

1. **Blend shape names are exact, case-sensitive strings** matched on the mesh:
   `Eyes Closed`, `Looking Up`, `Looking Down`, `Looking Left`, `Looking Right`,
   `Neck Twist Right`, `Neck Twist Left`, `Neck Bend Back`, `Neck Bend Front`.
2. **Serialized public field/method names of `Seiki`, `SeikiLookController`,
   `SeikiProfile` are a persistence contract** (referenced from prefabs/scenes).
   Renaming or removing them drops serialized data. If a rename is unavoidable, use
   `[FormerlySerializedAs("oldName")]` and confirm with the user first.
3. `ScmBoneAxis` int values (see above).
4. `Runtime/Internal/` is not public API.
5. Script execution orders −999999 and 15000 (stored in the callbacks' `.meta`
   files) — do not change.
6. Eyelid/SCM shapes are owned by `Seiki`; external code must not write them.
7. `RestoreDefault()` semantics: eyelids lerp from the authored default to the pose,
   never a hard 0.
8. No GC allocations in the per-frame chain (`Update` → `LateUpdate`/`Update1` →
   `VeryLateUpdate`/`Update2`): it runs per character per frame. No LINQ, closures,
   or per-frame `new` in that path.
9. The two hidden runtime-created gaze targets (`HideAndDontSave`, names
   `*_createdEyeTarget_1/2`) belong to `Seiki`; do not parent or destroy them.

## Usage pattern (Brain ↔ Seiki)

```csharp
using UnityEngine;
using Ozhogi.Seiki;

public class NpcBrain : MonoBehaviour
{
    Seiki seiki;
    SeikiLookController look;

    void OnEnable()
    {
        seiki = GetComponent<Seiki>();
        look  = GetComponent<SeikiLookController>();

        seiki.OnTargetDestroyed        += OnTargetLost;
        seiki.OnCannotGetTargetIntoView += OnCannotReach;
        seiki.OnUpdate2Finished        += OnEyesDone;
    }

    void OnDisable()
    {
        seiki.OnTargetDestroyed         -= OnTargetLost;
        seiki.OnCannotGetTargetIntoView -= OnCannotReach;
        seiki.OnUpdate2Finished         -= OnEyesDone;
    }

    void Update()
    {
        // Attention policy: move the home target. The controller commits
        // automatically and never drops it on its own.
        if (isReading)  look.defaultIdleTarget = book;
        if (isTalking)  look.defaultIdleTarget = otherNpc.head;

        // One-shot override: direct command, controller stays out of it.
        if (glanceRequested) seiki.LookAtSpecificThing(glanceTarget);
    }

    void OnEyesDone()
    {
        // Final eye state of this frame:
        Debug.DrawRay(seiki.EyesCombinedRay.origin, seiki.EyesCombinedRay.direction);
    }

    void OnTargetLost() => look.defaultIdleTarget = null;

    void OnCannotReach()
    {
        // Home left the reachable cone; re-point it or keep straining.
    }
}
```

## Code style (follow the existing files)

- Namespace `Ozhogi.Seiki`; runtime in `Runtime/`, editor-only code in `Editor/`.
- Allman braces, 4-space indent. Code comments in English.
- Public methods PascalCase; serialized public fields camelCase; content constants
  use the `k` prefix (`kBlinkTravelThreshold`).
- Internal "components" (`HeadComponent`, `ScmComponent`, `BlinkingComponent`,
  `BlendshapeBank`, `ControlData`) are plain classes in `Internal/`, **not**
  MonoBehaviours; `Seiki` owns them as `readonly` fields.
- Angles are normalized with `Utils.NormalizedDegAngle` (`Runtime/Internal/Utils.cs`).
- Events: C# `event Action`; subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- Inspector annotations: `[Header]`, `[Tooltip]`, `[Range]`; editor-managed cache:
  `[HideInInspector]`.
