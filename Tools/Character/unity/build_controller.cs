// Builds the player's animation set: a Shove clip assembled from two downloaded poses, an upper-body mask and the
// player controller (locomotion blend tree on measured clip speeds, jump/fall/land, hit reaction, shove layer).
const string DIR = "Assets/_Project/Animations/Character";
UnityEngine.AnimationClip Clip(string n) => UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>($"{DIR}/{n}.anim");

// ---- Shove: idle -> pulled-in push pose (windup) -> arms out (contact) -> hold -> idle. Muscles sampled on our avatar.
var preview = (UnityEngine.GameObject)UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/PlayerPreview.prefab"));
var animator = preview.GetComponent<UnityEngine.Animator>();
var handler = new UnityEngine.HumanPoseHandler(animator.avatar, preview.transform);
float[] Muscles(UnityEngine.AnimationClip clip, float time)
{
    // A playable graph applies humanoid clips in edit mode (SampleAnimation does not).
    var graph = UnityEngine.Playables.PlayableGraph.Create();
    var output = UnityEngine.Animations.AnimationPlayableOutput.Create(graph, "p", animator);
    var playable = UnityEngine.Animations.AnimationClipPlayable.Create(graph, clip);
    UnityEngine.Playables.PlayableOutputExtensions.SetSourcePlayable(output, playable);
    UnityEngine.Playables.PlayableExtensions.SetTime(playable, time);
    graph.Evaluate();
    var pose = new UnityEngine.HumanPose();
    handler.GetHumanPose(ref pose);
    graph.Destroy();
    return pose.muscles;
}
int M(string name) => System.Array.IndexOf(UnityEngine.HumanTrait.MuscleName, name);
float[] With(float[] pose, params (string muscle, float value)[] set) { var p = (float[])pose.Clone(); foreach (var (m, v) in set) p[M(m)] = v; return p; }
var idle = Muscles(Clip("Idle"), 0f);
// Poses set on an upright body (the shove layer keeps the legs' upright root; the downloaded push clip leans its whole
// root forward, which turned "arms forward" into "arms up"). Measured: Front-Back < 0 swings the arm forward;
// Down-Up 0.1 / Front-Back -0.7 puts the hands 0.4 m ahead at shoulder height; Spine/Chest < 0 lean forward.
(string, float)[] Arms(float downUp, float frontBack, float forearm) => new[] {
    ("Left Arm Down-Up", downUp), ("Right Arm Down-Up", downUp), ("Left Arm Front-Back", frontBack), ("Right Arm Front-Back", frontBack),
    ("Left Forearm Stretch", forearm), ("Right Forearm Stretch", forearm) };
float[] Pose((string, float)[] arms, float spine, float chest) => With(With(idle, arms), ("Spine Front-Back", spine), ("Chest Front-Back", chest));
// Windup: hands pulled to the chest, a little lean back. Contact: both arms straight out, leaning into it.
var windup = Pose(Arms(-0.1f, -0.35f, -0.7f), 0.12f, 0.08f);
var contact = Pose(Arms(0.1f, -0.75f, 1f), -0.25f, -0.15f);
var keys = new (float time, float[] pose)[] { (0f, idle), (0.08f, windup), (0.16f, contact), (0.26f, contact), (0.48f, idle) };
var shove = new UnityEngine.AnimationClip { name = "Shove" };
for (int m = 0; m < UnityEngine.HumanTrait.MuscleCount; m++)
{
    var curve = new UnityEngine.AnimationCurve();
    foreach (var (time, pose) in keys) curve.AddKey(new UnityEngine.Keyframe(time, pose[m]));
    for (int k = 0; k < curve.length; k++) UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, k, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
    for (int k = 0; k < curve.length; k++) UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, k, UnityEditor.AnimationUtility.TangentMode.ClampedAuto);
    shove.SetCurve("", typeof(UnityEngine.Animator), UnityEngine.HumanTrait.MuscleName[m], curve);
}
UnityEngine.Object.DestroyImmediate(preview);
// Contact frame (arms out, 0.16 s): PlayerAnimationController.OnShoveContact plays the whoosh.
UnityEditor.AnimationUtility.SetAnimationEvents(shove, new[] { new UnityEngine.AnimationEvent { time = 0.16f, functionName = "OnShoveContact" } });
UnityEditor.AssetDatabase.DeleteAsset($"{DIR}/Shove.anim");
UnityEditor.AssetDatabase.CreateAsset(shove, $"{DIR}/Shove.anim");

// ---- Upper body mask (torso, head, arms, fingers; not root or legs).
var mask = new UnityEngine.AvatarMask();
for (int i = 0; i < (int)UnityEngine.AvatarMaskBodyPart.LastBodyPart; i++)
{
    var part = (UnityEngine.AvatarMaskBodyPart)i;
    mask.SetHumanoidBodyPartActive(part, part is UnityEngine.AvatarMaskBodyPart.Body or UnityEngine.AvatarMaskBodyPart.Head
        or UnityEngine.AvatarMaskBodyPart.LeftArm or UnityEngine.AvatarMaskBodyPart.RightArm
        or UnityEngine.AvatarMaskBodyPart.LeftFingers or UnityEngine.AvatarMaskBodyPart.RightFingers);
}
UnityEditor.AssetDatabase.DeleteAsset($"{DIR}/UpperBody.mask");
UnityEditor.AssetDatabase.CreateAsset(mask, $"{DIR}/UpperBody.mask");

// ---- Controller.
const string CTRL = DIR + "/Player.controller";
UnityEditor.AssetDatabase.DeleteAsset(CTRL);
var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(CTRL);
ctrl.AddParameter("Speed", UnityEngine.AnimatorControllerParameterType.Float);
ctrl.AddParameter("LocoScale", UnityEngine.AnimatorControllerParameterType.Float);
ctrl.AddParameter("Grounded", UnityEngine.AnimatorControllerParameterType.Bool);
ctrl.AddParameter("Jump", UnityEngine.AnimatorControllerParameterType.Trigger);
ctrl.AddParameter("Hit", UnityEngine.AnimatorControllerParameterType.Trigger);
ctrl.AddParameter("Shove", UnityEngine.AnimatorControllerParameterType.Trigger);
foreach (var p in ctrl.parameters) if (p.name is "LocoScale") p.defaultFloat = 1f;
var ps = ctrl.parameters; foreach (var p in ps) { if (p.name == "LocoScale") p.defaultFloat = 1f; if (p.name == "Grounded") p.defaultBool = true; } ctrl.parameters = ps;

var layers = ctrl.layers; layers[0].iKPass = true; ctrl.layers = layers;
var sm = ctrl.layers[0].stateMachine;
var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
tree.blendType = UnityEditor.Animations.BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
// Thresholds = how fast each clip moves our character (measured with the root-motion versions): no foot sliding.
tree.AddChild(Clip("Idle"), 0f); tree.AddChild(Clip("Walk"), 0.757f); tree.AddChild(Clip("Jog"), 4.142f); tree.AddChild(Clip("Sprint"), 6.378f);
loco.speedParameterActive = true; loco.speedParameter = "LocoScale";
sm.defaultState = loco;
UnityEditor.Animations.AnimatorState State(string name, string clip) { var s = sm.AddState(name); s.motion = Clip(clip); return s; }
var jump = State("JumpStart", "JumpStart"); var fall = State("Fall", "Fall"); var land = State("Land", "Land"); var hit = State("Hit", "HitChest");
UnityEditor.Animations.AnimatorStateTransition T(UnityEditor.Animations.AnimatorState from, UnityEditor.Animations.AnimatorState to, float duration, float exitTime = -1f, float offset = 0f)
{
    var t = from.AddTransition(to); t.duration = duration; t.offset = offset; t.hasFixedDuration = true;
    t.hasExitTime = exitTime >= 0f; if (exitTime >= 0f) t.exitTime = exitTime;
    return t;
}
var anyHit = sm.AddAnyStateTransition(hit); anyHit.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Hit"); anyHit.duration = 0.05f; anyHit.canTransitionToSelf = false; anyHit.hasFixedDuration = true;
// Take-off skips the clip's crouch (our jump leaves the ground on the press).
foreach (var from in new[] { loco, land }) T(from, jump, 0.06f, -1f, 0.09f).AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Jump");
T(loco, fall, 0.15f).AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "Grounded");
T(jump, fall, 0.2f, 0.85f);
// Our jumps are shorter than the clip: land the moment the feet touch (the trigger fires once airborne).
var jumpLand = T(jump, land, 0.08f); jumpLand.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Grounded");
T(fall, land, 0.06f).AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Grounded");
T(land, loco, 0.2f, 0.35f);
T(land, loco, 0.15f).AddCondition(UnityEditor.Animations.AnimatorConditionMode.Greater, 1f, "Speed");
T(land, fall, 0.15f).AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "Grounded");
T(hit, fall, 0.15f, 0.9f).AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0, "Grounded");
T(hit, loco, 0.2f, 0.9f).AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Grounded");

// Shove: upper body over whatever the legs do.
ctrl.AddLayer("Shove");
layers = ctrl.layers; layers[1].defaultWeight = 1f; layers[1].avatarMask = mask; layers[1].blendingMode = UnityEditor.Animations.AnimatorLayerBlendingMode.Override; ctrl.layers = layers;
var sm1 = ctrl.layers[1].stateMachine;
var empty = sm1.AddState("Empty"); sm1.defaultState = empty;
var shoveState = sm1.AddState("Shove"); shoveState.motion = shove;
// Played at the speed that puts its contact frame on PushConfig.ContactDelay (set by PlayerAnimationController).
ctrl.AddParameter("ShoveSpeed", UnityEngine.AnimatorControllerParameterType.Float);
var withSpeed = ctrl.parameters; foreach (var p in withSpeed) if (p.name == "ShoveSpeed") p.defaultFloat = 1f; ctrl.parameters = withSpeed;
shoveState.speedParameterActive = true; shoveState.speedParameter = "ShoveSpeed";
var anyShove = sm1.AddAnyStateTransition(shoveState); anyShove.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Shove"); anyShove.duration = 0.03f; anyShove.hasFixedDuration = true; anyShove.canTransitionToSelf = true;
var back = shoveState.AddTransition(empty); back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.08f; back.hasFixedDuration = true;
// Emotes with a clip (EmotePlayer.HasClip): full body, weight driven by PlayerAnimationController (0 while moving).
ctrl.AddParameter("EmoteId", UnityEngine.AnimatorControllerParameterType.Int);
ctrl.AddParameter("Emote", UnityEngine.AnimatorControllerParameterType.Trigger);
ctrl.AddLayer("Emote");
layers = ctrl.layers; layers[2].defaultWeight = 0f; layers[2].blendingMode = UnityEditor.Animations.AnimatorLayerBlendingMode.Override; ctrl.layers = layers;
var sm2 = ctrl.layers[2].stateMachine;
var rest = sm2.AddState("Empty"); sm2.defaultState = rest;
foreach (var (id, clipName) in new[] { (1, "Emotes/Wave"), (2, "Dance"), (3, "Emotes/Laugh"), (4, "Emotes/Cheer"), (5, "Emotes/Cry"), (7, "Emotes/Clap") })
{
    var st = sm2.AddState(clipName.Replace("Emotes/", "")); st.motion = Clip(clipName); st.iKOnFeet = true;
    var go2 = sm2.AddAnyStateTransition(st);
    go2.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, "Emote");
    go2.AddCondition(UnityEditor.Animations.AnimatorConditionMode.Equals, id, "EmoteId");
    go2.duration = 0.15f; go2.hasFixedDuration = true; go2.canTransitionToSelf = true;
    if (clipName != "Dance") { var done = st.AddTransition(rest); done.hasExitTime = true; done.exitTime = 0.95f; done.duration = 0.2f; done.hasFixedDuration = true; }
}
UnityEditor.EditorUtility.SetDirty(ctrl);

// Clip loop settings for the in-place loops (copied clips keep them, but make sure).
foreach (var n in new[] { "Idle", "Walk", "Jog", "Sprint", "Fall" })
{
    var c = Clip(n); var s = UnityEditor.AnimationUtility.GetAnimationClipSettings(c);
    s.loopTime = true; UnityEditor.AnimationUtility.SetAnimationClipSettings(c, s);
}

// Use it on the player and the customize preview.
foreach (var path in new[] { "Assets/_Project/Prefabs/Player.prefab", "Assets/_Project/Prefabs/PlayerPreview.prefab" })
{
    var contents = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    foreach (var a in contents.GetComponentsInChildren<UnityEngine.Animator>(true)) a.runtimeAnimatorController = ctrl;
    UnityEditor.PrefabUtility.SaveAsPrefabAsset(contents, path);
    UnityEditor.PrefabUtility.UnloadPrefabContents(contents);
}
UnityEditor.AssetDatabase.SaveAssets();
return $"shove {shove.length:0.00}s, controller states={sm.states.Length}, layers={ctrl.layers.Length}";
