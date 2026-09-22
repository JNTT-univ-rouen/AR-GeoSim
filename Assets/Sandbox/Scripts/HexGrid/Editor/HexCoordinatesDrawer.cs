using UnityEngine;
using UnityEditor;

// Editor-only : doit rester dans un dossier Editor/, sinon UnityEditor
// (PropertyDrawer, SerializedProperty...) casse la compilation d'un vrai
// Build (seul l'Editeur lie UnityEditor.dll pour tous les scripts).
[CustomPropertyDrawer(typeof(HexCoordinates))]
public class HexCoordinatesDrawer : PropertyDrawer {
    public override void OnGUI (
        Rect position, SerializedProperty property, GUIContent label
    ) {
        HexCoordinates coordinates = new HexCoordinates(
            property.FindPropertyRelative("x").intValue,
            property.FindPropertyRelative("z").intValue
        );
        position = EditorGUI.PrefixLabel(position, label);
        GUI.Label(position, coordinates.ToString());
    }
}
