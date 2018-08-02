using UnityEngine;
using UnityEngine.Experimental.Rendering.HDPipeline;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    using _ = CoreEditorUtils;
    using CED = CoreEditorDrawer<InfluenceVolumeUI, SerializedInfluenceVolume>;

    partial class InfluenceVolumeUI
    {
        //[TODO: planar / non planar will be redone in next PR]
        public static readonly CED.IDrawer SectionFoldoutShapePlanar;
        public static readonly CED.IDrawer SectionFoldoutShape;
        public static readonly CED.IDrawer SectionShapeBoxPlanar = CED.Action((s, p, o) => Drawer_SectionShapeBox( s, p, o, false, false, false));
        public static readonly CED.IDrawer SectionShapeBox = CED.Action((s, p, o) => Drawer_SectionShapeBox(s, p, o, true, true, true));
        public static readonly CED.IDrawer SectionShapeSpherePlanar = CED.Action((s, p, o) => Drawer_SectionShapeSphere(s, p, o, false, false));
        public static readonly CED.IDrawer SectionShapeSphere = CED.Action((s, p, o) => Drawer_SectionShapeSphere(s, p, o, true, true));

        static InfluenceVolumeUI()
        {
            SectionFoldoutShapePlanar = CED.Group(
                    CED.FoldoutGroup(
                        "Influence Volume",
                        (s, d, o) => s.isSectionExpandedShape,
                        FoldoutOption.Indent,
                        CED.Action(Drawer_InfluenceAdvancedSwitch),
                        CED.space,
                        CED.Action(Drawer_FieldShapeType),
                        CED.FadeGroup(
                            (s, d, o, i) => s.IsSectionExpanded_Shape((InfluenceShape)i),
                            FadeOption.None,
                            SectionShapeBoxPlanar,
                            SectionShapeSpherePlanar
                            )
                        )
                    );
            SectionFoldoutShape = CED.Group(
                    CED.FoldoutGroup(
                        "Influence Volume",
                        (s, d, o) => s.isSectionExpandedShape,
                        FoldoutOption.Indent,
                        CED.Action(Drawer_InfluenceAdvancedSwitch),
                        CED.space,
                        CED.Action(Drawer_FieldShapeType),
                        CED.FadeGroup(
                            (s, d, o, i) => s.IsSectionExpanded_Shape((InfluenceShape)i),
                            FadeOption.None,
                            SectionShapeBox,
                            SectionShapeSphere
                            )
                        )
                    );
        }

        static void Drawer_FieldShapeType(InfluenceVolumeUI s, SerializedInfluenceVolume d, Editor o)
        {
            EditorGUILayout.PropertyField(d.shape, _.GetContent("Shape Type"));
        }

        static void Drawer_InfluenceAdvancedSwitch(InfluenceVolumeUI s, SerializedInfluenceVolume d, Editor owner)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                bool advanced = d.editorAdvancedModeEnabled.boolValue;
                advanced = !GUILayout.Toggle(!advanced, CoreEditorUtils.GetContent("Normal|Normal parameters mode (only change for box shape)."), EditorStyles.miniButtonLeft, GUILayout.Width(60f), GUILayout.ExpandWidth(false));
                advanced = GUILayout.Toggle(advanced, CoreEditorUtils.GetContent("Advanced|Advanced parameters mode (only change for box shape)."), EditorStyles.miniButtonRight, GUILayout.Width(60f), GUILayout.ExpandWidth(false));
                s.boxInfluenceHandle.allHandleControledByOne = s.boxInfluenceNormalHandle.allHandleControledByOne = !advanced;
                if (d.editorAdvancedModeEnabled.boolValue ^ advanced)
                {
                    d.editorAdvancedModeEnabled.boolValue = advanced;
                    if (advanced)
                    {
                        d.boxBlendDistancePositive.vector3Value = d.editorAdvancedModeBlendDistancePositive.vector3Value;
                        d.boxBlendDistanceNegative.vector3Value = d.editorAdvancedModeBlendDistanceNegative.vector3Value;
                        d.boxBlendNormalDistancePositive.vector3Value = d.editorAdvancedModeBlendNormalDistancePositive.vector3Value;
                        d.boxBlendNormalDistanceNegative.vector3Value = d.editorAdvancedModeBlendNormalDistanceNegative.vector3Value;
                    }
                    else
                    {
                        d.boxBlendDistanceNegative.vector3Value = d.boxBlendDistancePositive.vector3Value = Vector3.one * d.editorSimplifiedModeBlendDistance.floatValue;
                        d.boxBlendNormalDistanceNegative.vector3Value = d.boxBlendNormalDistancePositive.vector3Value = Vector3.one * d.editorSimplifiedModeBlendNormalDistance.floatValue;
                    }
                    d.Apply();
                }
            }
        }

        static void Drawer_SectionShapeBox(InfluenceVolumeUI s, SerializedInfluenceVolume d, Editor o, bool drawOffset, bool drawNormal, bool drawFace)
        {
            bool advanced = d.editorAdvancedModeEnabled.boolValue;
            var maxFadeDistance = d.boxSize.vector3Value * 0.5f;
            var minFadeDistance = Vector3.zero;

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(d.boxSize, _.GetContent("Box Size"));
            if (EditorGUI.EndChangeCheck())
            {
                Vector3 blendPositive = d.boxBlendDistancePositive.vector3Value;
                Vector3 blendNegative = d.boxBlendDistanceNegative.vector3Value;
                Vector3 blendNormalPositive = d.boxBlendNormalDistancePositive.vector3Value;
                Vector3 blendNormalNegative = d.boxBlendNormalDistanceNegative.vector3Value;
                Vector3 size = d.boxSize.vector3Value;
                for(int i = 0; i<3; ++i)
                {
                    size[i] = Mathf.Max(0f, size[i]);
                }
                d.boxSize.vector3Value = size;
                Vector3 halfSize = size * .5f;
                for (int i = 0; i < 3; ++i)
                {
                    blendPositive[i] = Mathf.Clamp(blendPositive[i], 0f, halfSize[i]);
                    blendNegative[i] = Mathf.Clamp(blendNegative[i], 0f, halfSize[i]);
                    blendNormalPositive[i] = Mathf.Clamp(blendNormalPositive[i], 0f, halfSize[i]);
                    blendNormalNegative[i] = Mathf.Clamp(blendNormalNegative[i], 0f, halfSize[i]);
                }
                d.boxBlendDistancePositive.vector3Value = blendPositive;
                d.boxBlendDistanceNegative.vector3Value = blendNegative;
                d.boxBlendNormalDistancePositive.vector3Value = blendNormalPositive;
                d.boxBlendNormalDistanceNegative.vector3Value = blendNormalNegative;
                if (d.editorAdvancedModeEnabled.boolValue)
                {
                    d.editorAdvancedModeBlendDistancePositive.vector3Value = d.boxBlendDistancePositive.vector3Value;
                    d.editorAdvancedModeBlendDistanceNegative.vector3Value = d.boxBlendDistanceNegative.vector3Value;
                    d.editorAdvancedModeBlendNormalDistancePositive.vector3Value = d.boxBlendNormalDistancePositive.vector3Value;
                    d.editorAdvancedModeBlendNormalDistanceNegative.vector3Value = d.boxBlendNormalDistanceNegative.vector3Value;
                }
                else
                {
                    d.editorSimplifiedModeBlendDistance.floatValue = Mathf.Min(blendPositive.x, blendPositive.y, blendPositive.z, blendNegative.x, blendNegative.y, blendNegative.z);
                    d.boxBlendDistancePositive.vector3Value = d.boxBlendDistanceNegative.vector3Value = Vector3.one * d.editorSimplifiedModeBlendDistance.floatValue;
                    d.editorSimplifiedModeBlendNormalDistance.floatValue = Mathf.Min(blendNormalPositive.x, blendNormalPositive.y, blendNormalPositive.z, blendNormalNegative.x, blendNormalNegative.y, blendNormalNegative.z);
                    d.boxBlendNormalDistancePositive.vector3Value = d.boxBlendNormalDistanceNegative.vector3Value = Vector3.one * d.editorSimplifiedModeBlendNormalDistance.floatValue;
                }
            }
            HDProbeUI.Drawer_ToolBarButton(HDProbeUI.ToolBar.InfluenceShape, o, GUILayout.Width(28f), GUILayout.MinHeight(22f));
            EditorGUILayout.EndHorizontal();

            if (drawOffset)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(d.offset, _.GetContent("Offset"));
                HDProbeUI.Drawer_ToolBarButton(HDProbeUI.ToolBar.CapturePosition, o, GUILayout.Width(28f), GUILayout.MinHeight(22f));
                EditorGUILayout.EndHorizontal();
            }
            
            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing);
            
            EditorGUILayout.BeginHorizontal();
            Drawer_AdvancedBlendDistance(
                d,
                false,
                maxFadeDistance,
                CoreEditorUtils.GetContent("Blend Distance|Area around the probe where it is blended with other probes. Only used in deferred probes.")
                );
            HDProbeUI.Drawer_ToolBarButton(HDProbeUI.ToolBar.Blend, o, GUILayout.ExpandHeight(true), GUILayout.Width(28f), GUILayout.MinHeight(22f), GUILayout.MaxHeight((advanced ? 2 : 1) * (EditorGUIUtility.singleLineHeight + 3)));
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Space(EditorGUIUtility.standardVerticalSpacing * 2f);

            if (drawNormal)
            {
                EditorGUILayout.BeginHorizontal();
                Drawer_AdvancedBlendDistance(
                    d,
                    true,
                    maxFadeDistance,
                    CoreEditorUtils.GetContent("Blend Normal Distance|Area around the probe where the normals influence the probe. Only used in deferred probes.")
                    );
                HDProbeUI.Drawer_ToolBarButton(HDProbeUI.ToolBar.NormalBlend, o, GUILayout.ExpandHeight(true), GUILayout.Width(28f), GUILayout.MinHeight(22f), GUILayout.MaxHeight((advanced ? 2 : 1) * (EditorGUIUtility.singleLineHeight + 3)));
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(EditorGUIUtility.standardVerticalSpacing * 2f);
            }

            if (advanced && drawFace)
            {
                EditorGUILayout.BeginHorizontal();
                CoreEditorUtils.DrawVector6(
                    CoreEditorUtils.GetContent("Face fade|Fade faces of the cubemap."),
                    d.boxSideFadePositive, d.boxSideFadeNegative, Vector3.zero, Vector3.one, HDReflectionProbeEditor.k_handlesColor);
                GUILayout.Space(28f + 9f); //add right margin for alignment
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(EditorGUIUtility.standardVerticalSpacing * 2f);
            }
        }

        static void Drawer_AdvancedBlendDistance(SerializedInfluenceVolume d, bool isNormal, Vector3 maxBlendDistance, GUIContent content)
        {
            SerializedProperty blendDistancePositive = isNormal ? d.boxBlendNormalDistancePositive : d.boxBlendDistancePositive;
            SerializedProperty blendDistanceNegative = isNormal ? d.boxBlendNormalDistanceNegative : d.boxBlendDistanceNegative;
            SerializedProperty editorAdvancedModeBlendDistancePositive = isNormal ? d.editorAdvancedModeBlendNormalDistancePositive : d.editorAdvancedModeBlendDistancePositive;
            SerializedProperty editorAdvancedModeBlendDistanceNegative = isNormal ? d.editorAdvancedModeBlendNormalDistanceNegative : d.editorAdvancedModeBlendDistanceNegative;
            SerializedProperty editorSimplifiedModeBlendDistance = isNormal ? d.editorSimplifiedModeBlendNormalDistance : d.editorSimplifiedModeBlendDistance;
            Vector3 bdp = blendDistancePositive.vector3Value;
            Vector3 bdn = blendDistanceNegative.vector3Value;

            EditorGUILayout.BeginVertical();

            if (d.editorAdvancedModeEnabled.boolValue)
            {
                EditorGUI.BeginChangeCheck();
                blendDistancePositive.vector3Value = editorAdvancedModeBlendDistancePositive.vector3Value;
                blendDistanceNegative.vector3Value = editorAdvancedModeBlendDistanceNegative.vector3Value;
                CoreEditorUtils.DrawVector6(
                    content,
                    blendDistancePositive, blendDistanceNegative, Vector3.zero, maxBlendDistance, HDReflectionProbeEditor.k_handlesColor);
                if(EditorGUI.EndChangeCheck())
                {
                    editorAdvancedModeBlendDistancePositive.vector3Value = blendDistancePositive.vector3Value;
                    editorAdvancedModeBlendDistanceNegative.vector3Value = blendDistanceNegative.vector3Value;
                }
            }
            else
            {
                float distance = editorSimplifiedModeBlendDistance.floatValue;
                EditorGUI.BeginChangeCheck();
                distance = EditorGUILayout.FloatField(content, distance);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 decal = Vector3.one * distance;
                    bdp.x = Mathf.Clamp(decal.x, 0f, maxBlendDistance.x);
                    bdp.y = Mathf.Clamp(decal.y, 0f, maxBlendDistance.y);
                    bdp.z = Mathf.Clamp(decal.z, 0f, maxBlendDistance.z);
                    bdn.x = Mathf.Clamp(decal.x, 0f, maxBlendDistance.x);
                    bdn.y = Mathf.Clamp(decal.y, 0f, maxBlendDistance.y);
                    bdn.z = Mathf.Clamp(decal.z, 0f, maxBlendDistance.z);
                    blendDistancePositive.vector3Value = bdp;
                    blendDistanceNegative.vector3Value = bdn;
                    editorSimplifiedModeBlendDistance.floatValue = distance;
                }
            }

            GUILayout.EndVertical();
        }

        static void Drawer_SectionShapeSphere(InfluenceVolumeUI s, SerializedInfluenceVolume d, Editor o, bool drawOffset, bool drawNormal)
        {

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(d.sphereRadius, _.GetContent("Radius"));
            HDProbeUI.Drawer_ToolBarButton(HDProbeUI.ToolBar.InfluenceShape, o, GUILayout.Width(28f), GUILayout.MinHeight(22f));
            EditorGUILayout.EndHorizontal();

            if(drawOffset)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(d.offset, _.GetContent("Offset"));
                HDReflectionProbeUI.Drawer_ToolBarButton(HDProbeUI.ToolBar.CapturePosition, o, GUILayout.Width(28f), GUILayout.MinHeight(22f));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            var maxBlendDistance = d.sphereRadius.floatValue;

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(d.sphereBlendDistance, _.GetContent("Blend Distance|Area around the probe where it is blended with other probes. Only used in deferred probes."));
            if (EditorGUI.EndChangeCheck())
            {
                d.sphereBlendDistance.floatValue = Mathf.Clamp(d.sphereBlendDistance.floatValue, 0, maxBlendDistance);
            }
            HDProbeUI.Drawer_ToolBarButton(HDProbeUI.ToolBar.Blend, o, GUILayout.ExpandHeight(true), GUILayout.Width(28f), GUILayout.MinHeight(22f), GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight + 3));
            EditorGUILayout.EndHorizontal();

            if (drawNormal)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(d.sphereBlendNormalDistance, _.GetContent("Blend Distance|Area around the probe where it is blended with other probes. Only used in deferred probes."));
                if (EditorGUI.EndChangeCheck())
                {
                    d.sphereBlendNormalDistance.floatValue = Mathf.Clamp(d.sphereBlendNormalDistance.floatValue, 0, maxBlendDistance);
                }
                HDProbeUI.Drawer_ToolBarButton(HDProbeUI.ToolBar.NormalBlend, o, GUILayout.ExpandHeight(true), GUILayout.Width(28f), GUILayout.MinHeight(22f), GUILayout.MaxHeight(EditorGUIUtility.singleLineHeight + 3));
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
