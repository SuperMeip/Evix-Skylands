using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Collections.ObjectModel;
using System.Reflection;
using UnityEditor;
using Object = UnityEngine.Object;
using UnityEngine.SceneManagement;
using System.IO;

namespace Evix.EditorTools {

	/// <summary>
	/// Conditionally Show/Hide field in inspector, based on some other field value 
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class ConditionalFieldAttribute : PropertyAttribute {
		public readonly string FieldToCheck;
		public readonly string[] CompareValues;
		public readonly bool Inverse;

		/// <param name="fieldToCheck">String name of field to check value</param>
		/// <param name="inverse">Inverse check result</param>
		/// <param name="compareValues">On which values field will be shown in inspector</param>
		public ConditionalFieldAttribute(string fieldToCheck, bool inverse = false, params object[] compareValues) {
			FieldToCheck = fieldToCheck;
			Inverse = inverse;
			CompareValues = compareValues.Select(c => c.ToString().ToUpper()).ToArray();
		}
	}

#if UNITY_EDITOR

	[CustomPropertyDrawer(typeof(ConditionalFieldAttribute))]
	public class ConditionalFieldAttributeDrawer : PropertyDrawer {
		private ConditionalFieldAttribute Conditional => _conditional ?? (_conditional = attribute as ConditionalFieldAttribute);
		private ConditionalFieldAttribute _conditional;

		private bool _customDrawersCached;
		private static IEnumerable<Type> _allPropertyDrawerAttributeTypes;
		private bool _multipleAttributes;
		private bool _specialType;
		private PropertyAttribute _genericAttribute;
		private PropertyDrawer _genericAttributeDrawerInstance;
		private Type _genericAttributeDrawerType;
		private Type _genericType;
		private PropertyDrawer _genericTypeDrawerInstance;
		private Type _genericTypeDrawerType;


		/// <summary>
		/// If conditional is part of type in collection, we need to link properties as in collection
		/// </summary>
		private readonly Dictionary<SerializedProperty, SerializedProperty> _conditionalToTarget =
			new Dictionary<SerializedProperty, SerializedProperty>();

		private bool _toShow = true;


		private void Initialize(SerializedProperty property) {
			if (!_conditionalToTarget.ContainsKey(property))
				_conditionalToTarget.Add(property, ConditionalFieldUtility.FindRelativeProperty(property, Conditional.FieldToCheck));

			if (_customDrawersCached) return;
			if (_allPropertyDrawerAttributeTypes == null) {
				_allPropertyDrawerAttributeTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes())
					.Where(x => typeof(PropertyDrawer).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract);
			}

			if (HaveMultipleAttributes()) {
				_multipleAttributes = true;
				GetPropertyDrawerType(property);
			} else if (fieldInfo != null && !fieldInfo.FieldType.Module.ScopeName.Equals(typeof(int).Module.ScopeName)) {
				_specialType = true;
				GetTypeDrawerType(property);
			}

			_customDrawersCached = true;
		}

		private bool HaveMultipleAttributes() {
			if (fieldInfo == null) return false;
			var genericAttributeType = typeof(PropertyAttribute);
			var attributes = fieldInfo.GetCustomAttributes(genericAttributeType, false);
			if (attributes == null || attributes.Length == 0) return false;
			return attributes.Length > 1;
		}


		public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
			Initialize(property);

			_toShow = ConditionalFieldUtility.PropertyIsVisible(_conditionalToTarget[property], Conditional.Inverse, Conditional.CompareValues);
			if (!_toShow) return 0;

			if (_genericAttributeDrawerInstance != null)
				return _genericAttributeDrawerInstance.GetPropertyHeight(property, label);

			if (_genericTypeDrawerInstance != null)
				return _genericTypeDrawerInstance.GetPropertyHeight(property, label);

			return EditorGUI.GetPropertyHeight(property);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			if (!_toShow) return;

			if (_multipleAttributes && _genericAttributeDrawerInstance != null) {
				try {
					_genericAttributeDrawerInstance.OnGUI(position, property, label);
				} catch (Exception e) {
					EditorGUI.PropertyField(position, property, label);
					LogWarning("Unable to instantiate " + _genericAttribute.GetType() + " : " + e, property);
				}
			} else if (_specialType && _genericTypeDrawerInstance != null) {
				try {
					_genericTypeDrawerInstance.OnGUI(position, property, label);
				} catch (Exception e) {
					EditorGUI.PropertyField(position, property, label);
					LogWarning("Unable to instantiate " + _genericType + " : " + e, property);
				}
			} else {
				EditorGUI.PropertyField(position, property, label, true);
			}
		}

		private void LogWarning(string log, SerializedProperty property) {
			var warning = "Property <color=brown>" + fieldInfo.Name + "</color>";
			if (fieldInfo != null && fieldInfo.DeclaringType != null)
				warning += " on behaviour <color=brown>" + fieldInfo.DeclaringType.Name + "</color>";
			warning += " caused: " + log;

			World.Debug.logWarning(warning);
		}


		#region Get Custom Property/Type drawers

		private void GetPropertyDrawerType(SerializedProperty property) {
			if (_genericAttributeDrawerInstance != null) return;

			//Get the second attribute flag
			try {
				_genericAttribute = (PropertyAttribute)fieldInfo.GetCustomAttributes(typeof(PropertyAttribute), false)
					.FirstOrDefault(a => !(a is ConditionalFieldAttribute));

				if (_genericAttribute is ContextMenuItemAttribute ||
						_genericAttribute is SeparatorAttribute | _genericAttribute is AutoPropertyAttribute) {
					LogWarning("[ConditionalField] does not work with " + _genericAttribute.GetType(), property);
					return;
				}

				if (_genericAttribute is TooltipAttribute) return;
			} catch (Exception e) {
				LogWarning("Can't find stacked propertyAttribute after ConditionalProperty: " + e, property);
				return;
			}

			//Get the associated attribute drawer
			try {
				_genericAttributeDrawerType = _allPropertyDrawerAttributeTypes.First(x =>
					(Type)CustomAttributeData.GetCustomAttributes(x).First().ConstructorArguments.First().Value == _genericAttribute.GetType());
			} catch (Exception e) {
				LogWarning("Can't find property drawer from CustomPropertyAttribute of " + _genericAttribute.GetType() + " : " + e, property);
				return;
			}

			//Create instances of each (including the arguments)
			try {
				_genericAttributeDrawerInstance = (PropertyDrawer)Activator.CreateInstance(_genericAttributeDrawerType);
				//Get arguments
				IList<CustomAttributeTypedArgument> attributeParams = fieldInfo.GetCustomAttributesData()
				.First(a => a.AttributeType == _genericAttribute.GetType()).ConstructorArguments;
				IList<CustomAttributeTypedArgument> unpackedParams = new List<CustomAttributeTypedArgument>();
				//Unpack any params object[] args
				foreach (CustomAttributeTypedArgument singleParam in attributeParams) {
					if (singleParam.Value.GetType() == typeof(ReadOnlyCollection<CustomAttributeTypedArgument>)) {
						foreach (CustomAttributeTypedArgument unpackedSingleParam in (ReadOnlyCollection<CustomAttributeTypedArgument>)singleParam
							.Value) {
							unpackedParams.Add(unpackedSingleParam);
						}
					} else {
						unpackedParams.Add(singleParam);
					}
				}

				object[] attributeParamsObj = unpackedParams.Select(x => x.Value).ToArray();

				if (attributeParamsObj.Any()) {
					_genericAttribute = (PropertyAttribute)Activator.CreateInstance(_genericAttribute.GetType(), attributeParamsObj);
				} else {
					_genericAttribute = (PropertyAttribute)Activator.CreateInstance(_genericAttribute.GetType());
				}
			} catch (Exception e) {
				LogWarning("No constructor available in " + _genericAttribute.GetType() + " : " + e, property);
				return;
			}

			//Reassign the attribute field in the drawer so it can access the argument values
			try {
				var genericDrawerAttributeField = _genericAttributeDrawerType.GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic);
				genericDrawerAttributeField.SetValue(_genericAttributeDrawerInstance, _genericAttribute);
			} catch (Exception e) {
				LogWarning("Unable to assign attribute to " + _genericAttributeDrawerInstance.GetType() + " : " + e, property);
			}
		}


		private void GetTypeDrawerType(SerializedProperty property) {
			if (_genericTypeDrawerInstance != null) return;

			//Get the associated attribute drawer
			try {
				// Of all property drawers in the assembly we need to find one that affects target type
				// or one of the base types of target type
				foreach (Type propertyDrawerType in _allPropertyDrawerAttributeTypes) {
					_genericType = fieldInfo.FieldType;
					var affectedType = (Type)CustomAttributeData.GetCustomAttributes(propertyDrawerType).First().ConstructorArguments.First().Value;
					while (_genericType != null) {
						if (_genericTypeDrawerType != null) break;
						if (affectedType == _genericType) _genericTypeDrawerType = propertyDrawerType;
						else _genericType = _genericType.BaseType;
					}
					if (_genericTypeDrawerType != null) break;
				}
			} catch (Exception) {
				// Commented out because of multiple false warnings on Behaviour types
				//LogWarning("[ConditionalField] does not work with "+_genericType+". Unable to find property drawer from the Type", property);
				return;
			}
			if (_genericTypeDrawerType == null) return;

			//Create instances of each (including the arguments)
			try {
				_genericTypeDrawerInstance = (PropertyDrawer)Activator.CreateInstance(_genericTypeDrawerType);
			} catch (Exception e) {
				LogWarning("no constructor available in " + _genericType + " : " + e, property);
				return;
			}

			//Reassign the attribute field in the drawer so it can access the argument values
			try {
				_genericTypeDrawerType.GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic)
					.SetValue(_genericTypeDrawerInstance, fieldInfo);
			} catch (Exception) {
				//LogWarning("Unable to assign attribute to " + _genericTypeDrawerInstance.GetType() + " : " + e, property);
			}
		}

		#endregion
	}

	public static class ConditionalFieldUtility {
		#region Property Is Visible

		public static bool PropertyIsVisible(SerializedProperty property, bool inverse, string[] compareAgainst) {
			if (property == null) return true;

			string asString = property.AsStringValue().ToUpper();

			if (compareAgainst != null && compareAgainst.Length > 0) {
				var matchAny = CompareAgainstValues(asString, compareAgainst);
				if (inverse) matchAny = !matchAny;
				return matchAny;
			}

			bool someValueAssigned = asString != "FALSE" && asString != "0" && asString != "NULL";
			if (someValueAssigned) return !inverse;

			return inverse;
		}

		/// <summary>
		/// True if the property value matches any of the values in '_compareValues'
		/// </summary>
		private static bool CompareAgainstValues(string propertyValueAsString, string[] compareAgainst) {
			for (var i = 0; i < compareAgainst.Length; i++) {
				bool valueMatches = compareAgainst[i] == propertyValueAsString;

				// One of the value is equals to the property value.
				if (valueMatches) return true;
			}

			// None of the value is equals to the property value.
			return false;
		}

		#endregion


		#region Find Relative Property

		public static SerializedProperty FindRelativeProperty(SerializedProperty property, string propertyName) {
			if (property.depth == 0) return property.serializedObject.FindProperty(propertyName);

			var path = property.propertyPath.Replace(".Array.data[", "[");
			var elements = path.Split('.');

			var nestedProperty = NestedPropertyOrigin(property, elements);

			// if nested property is null = we hit an array property
			if (nestedProperty == null) {
				var cleanPath = path.Substring(0, path.IndexOf('['));
				var arrayProp = property.serializedObject.FindProperty(cleanPath);
				var target = arrayProp.serializedObject.targetObject;

				var who = "Property <color=brown>" + arrayProp.name + "</color> in object <color=brown>" + target.name + "</color> caused: ";
				var warning = who + "Array fields is not supported by [ConditionalFieldAttribute]";

				World.Debug.logWarning(warning);

				return null;
			}

			return nestedProperty.FindPropertyRelative(propertyName);
		}

		// For [Serialized] types with [Conditional] fields
		private static SerializedProperty NestedPropertyOrigin(SerializedProperty property, string[] elements) {
			SerializedProperty parent = null;

			for (int i = 0; i < elements.Length - 1; i++) {
				var element = elements[i];
				int index = -1;
				if (element.Contains("[")) {
					index = Convert.ToInt32(element.Substring(element.IndexOf("[", StringComparison.Ordinal))
						.Replace("[", "").Replace("]", ""));
					element = element.Substring(0, element.IndexOf("[", StringComparison.Ordinal));
				}

				parent = i == 0
					? property.serializedObject.FindProperty(element)
					: parent != null
						? parent.FindPropertyRelative(element)
						: null;

				if (index >= 0 && parent != null) parent = parent.GetArrayElementAtIndex(index);
			}

			return parent;
		}

		#endregion

		#region Behaviour Property Is Visible

		public static bool BehaviourPropertyIsVisible(MonoBehaviour behaviour, string propertyName, ConditionalFieldAttribute appliedAttribute) {
			if (string.IsNullOrEmpty(appliedAttribute.FieldToCheck)) return true;

			var so = new SerializedObject(behaviour);
			var property = so.FindProperty(propertyName);
			var targetProperty = FindRelativeProperty(property, appliedAttribute.FieldToCheck);

			return PropertyIsVisible(targetProperty, appliedAttribute.Inverse, appliedAttribute.CompareValues);
		}

		#endregion
	}
#endif

	public class SeparatorAttribute : PropertyAttribute {
		public readonly string Title;
		public readonly bool WithOffset;


		public SeparatorAttribute() {
			Title = "";
		}

		public SeparatorAttribute(string title, bool withOffset = false) {
			Title = title;
			WithOffset = withOffset;
		}
	}

#if UNITY_EDITOR
	[CustomPropertyDrawer(typeof(SeparatorAttribute))]
	public class SeparatorAttributeDrawer : DecoratorDrawer {
		private SeparatorAttribute Separator {
			get { return (SeparatorAttribute)attribute; }
		}

		public override void OnGUI(Rect position) {
			var title = Separator.Title;
			if (title == "") {
				position.height = 1;
				position.y += 19;
				GUI.Box(position, "");
			} else {
				Vector2 textSize = GUI.skin.label.CalcSize(new GUIContent(title));
				float separatorWidth = (position.width - textSize.x) / 2.0f - 5.0f;
				position.y += 19;

				GUI.Box(new Rect(position.xMin, position.yMin, separatorWidth, 1), "");
				GUI.Label(new Rect(position.xMin + separatorWidth + 5.0f, position.yMin - 8.0f, textSize.x, 20), title);
				GUI.Box(new Rect(position.xMin + separatorWidth + 10.0f + textSize.x, position.yMin, separatorWidth, 1), "");
			}
		}

		public override float GetHeight() {
			return Separator.WithOffset ? 36.0f : 26f;
		}
	}
#endif

	/// <summary>
	/// Automatically assign components from this GO to this Property.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public class AutoPropertyAttribute : PropertyAttribute {
	}

#if UNITY_EDITOR

	[CustomPropertyDrawer(typeof(AutoPropertyAttribute))]
	public class AutoPropertyDrawer : PropertyDrawer {
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
			GUI.enabled = false;
			EditorGUI.PropertyField(position, property, label);
			GUI.enabled = true;
		}
	}


	[InitializeOnLoad]
	public static class AutoPropertyHandler {
		static AutoPropertyHandler() {
			MyEditorEvents.OnSave += CheckComponents;
		}

		private static void CheckComponents() {
			var autoProperties = MyEditor.GetFieldsWithAttribute<AutoPropertyAttribute>();
			for (var i = 0; i < autoProperties.Length; i++) {
				FillProperty(autoProperties[i]);
			}
		}

		private static void FillProperty(MyEditor.ComponentField property) {
			var propertyType = property.Field.FieldType;

			if (property.Field.FieldType.IsArray) {
				var underlyingType = propertyType.GetElementType();
				Object[] components = property.Component.GetComponentsInChildren(underlyingType, true);
				if (components != null && components.Length > 0) {
					var serializedObject = new SerializedObject(property.Component);
					var serializedProperty = serializedObject.FindProperty(property.Field.Name);
					serializedProperty.ReplaceArray(components);
					serializedObject.ApplyModifiedProperties();
					return;
				}
			} else {
				var component = property.Component.GetComponentInChildren(propertyType, true);
				if (component != null) {
					var serializedObject = new SerializedObject(property.Component);
					var serializedProperty = serializedObject.FindProperty(property.Field.Name);
					serializedProperty.objectReferenceValue = component;
					serializedObject.ApplyModifiedProperties();
					return;
				}
			}

			Debug.LogError(string.Format("{0} caused: {1} is failed to Auto Assign property. No match",
					property.Component.name, property.Field.Name),
				property.Component.gameObject);
		}
	}

	[InitializeOnLoad]
	public class MyEditorEvents : UnityEditor.AssetModificationProcessor {
		/// <summary>
		/// Occurs on Scenes/Assets Save
		/// </summary>
		public static Action OnSave;

		/// <summary>
		/// Occurs on first frame in Playmode
		/// </summary>
		public static Action OnFirstFrame;

		public static Action BeforePlaymode;

		public static Action BeforeBuild;


		static MyEditorEvents() {
			EditorApplication.update += CheckOnce;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}


		/// <summary>
		/// On Editor Save
		/// </summary>
		private static string[] OnWillSaveAssets(string[] paths) {
			// Prefab creation enforces SaveAsset and this may cause unwanted dir cleanup
			if (paths.Length == 1 && (paths[0] == null || paths[0].EndsWith(".prefab"))) return paths;

			if (OnSave != null) OnSave();

			return paths;
		}

		/// <summary>
		/// On First Frame
		/// </summary>
		private static void CheckOnce() {
			if (Application.isPlaying) {
				EditorApplication.update -= CheckOnce;
				if (OnFirstFrame != null) OnFirstFrame();
			}
		}

		/// <summary>
		/// On Before Playmode
		/// </summary>
		private static void OnPlayModeStateChanged(PlayModeStateChange state) {
			if (state == PlayModeStateChange.ExitingEditMode && BeforePlaymode != null) BeforePlaymode();
		}

		public int callbackOrder {
			get { return 0; }
		}
	}

	public static class ExtentionMethods {
		/// <summary>
		/// Get string representation of serialized property
		/// </summary>
		public static string AsStringValue(this SerializedProperty property) {
			switch (property.propertyType) {
				case SerializedPropertyType.String:
					return property.stringValue;

				case SerializedPropertyType.Character:
				case SerializedPropertyType.Integer:
					if (property.type == "char") return Convert.ToChar(property.intValue).ToString();
					return property.intValue.ToString();

				case SerializedPropertyType.ObjectReference:
					return property.objectReferenceValue != null ? property.objectReferenceValue.ToString() : "null";

				case SerializedPropertyType.Boolean:
					return property.boolValue.ToString();

				case SerializedPropertyType.Enum:
					return property.enumNames[property.enumValueIndex];

				default:
					return string.Empty;
			}
		}

		/// <summary>
		/// Replace array contents of SerializedProperty with another array 
		/// </summary>
		public static void ReplaceArray(this SerializedProperty property, Object[] newElements) {
			property.arraySize = 0;
			property.serializedObject.ApplyModifiedProperties();
			property.arraySize = newElements.Length;
			for (var i = 0; i < newElements.Length; i++) {
				property.GetArrayElementAtIndex(i).objectReferenceValue = newElements[i];
			}

			property.serializedObject.ApplyModifiedProperties();
		}
	}

	public static class MyEditor {
		#region Hierarchy Management

		/// <summary>
		/// Fold/Unfold GameObject hierarchy
		/// </summary>
		public static void FoldInHierarchy(GameObject go, bool expand) {
			if (go == null) return;
			var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
			var methodInfo = type.GetMethod("SetExpandedRecursive");
			if (methodInfo == null) return;

			EditorApplication.ExecuteMenuItem("Window/General/Hierarchy");
			var window = EditorWindow.focusedWindow;

			methodInfo.Invoke(window, new object[] { go.GetInstanceID(), expand });
		}

		/// <summary>
		/// Fold objects hierarchy for all opened scenes
		/// </summary>
		public static void FoldSceneHierarchy() {
			for (var i = 0; i < SceneManager.sceneCount; i++) {
				var scene = SceneManager.GetSceneAt(i);
				if (!scene.isLoaded) continue;
				var roots = SceneManager.GetSceneAt(i).GetRootGameObjects();
				for (var o = 0; o < roots.Length; o++) {
					FoldInHierarchy(roots[o], false);
				}
			}
		}

		#endregion


		#region GameObject Rename Mode

		/// <summary>
		/// Set currently selected object to Rename Mode
		/// </summary>
		public static void InitiateObjectRename(GameObject objectToRename) {
			EditorApplication.update += ObjectRename;
			_renameTimestamp = EditorApplication.timeSinceStartup + 0.4d;
			EditorApplication.ExecuteMenuItem("Window/Hierarchy");
			Selection.activeGameObject = objectToRename;
		}

		private static void ObjectRename() {
			if (EditorApplication.timeSinceStartup >= _renameTimestamp) {
				EditorApplication.update -= ObjectRename;
				var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
				var hierarchyWindow = EditorWindow.GetWindow(type);
				var renameMethod = type.GetMethod("RenameGO", BindingFlags.Instance | BindingFlags.NonPublic);
				if (renameMethod == null) {
					Debug.LogError("RenameGO method is obsolete?");
					return;
				}

				renameMethod.Invoke(hierarchyWindow, null);
			}
		}

		private static double _renameTimestamp;

		#endregion

		#region Prefab Management

		/// <summary>
		/// Apply changes on GameObject to prefab
		/// </summary>
		public static void ApplyPrefab(GameObject instance) {
#pragma warning disable CS0618 // Type or member is obsolete
			var instanceRoot = PrefabUtility.FindRootGameObjectWithSameParentPrefab(instance);
#pragma warning restore CS0618 // Type or member is obsolete

			var targetPrefab = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);

			if (instanceRoot == null || targetPrefab == null) {
				Debug.LogError("ApplyPrefab failed. Target object " + instance.name + " is not a prefab");
				return;
			}

#pragma warning disable CS0618 // Type or member is obsolete
			PrefabUtility.ReplacePrefab(instanceRoot, targetPrefab, ReplacePrefabOptions.ConnectToPrefab);
#pragma warning restore CS0618 // Type or member is obsolete
		}

		/// <summary>
		/// Get Prefab path in Asset Database
		/// </summary>
		/// <returns>Null if not a prefab</returns>
		public static string GetPrefabPath(GameObject gameObject, bool withAssetName = true) {
			if (gameObject == null) return null;

			Object prefabParent = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
			if (prefabParent == null) return null;
			var assetPath = AssetDatabase.GetAssetPath(prefabParent);

			return !withAssetName ? Path.GetDirectoryName(assetPath) : assetPath;
		}

		public static bool IsPrefabInstance(this GameObject go) {
#pragma warning disable CS0618 // Type or member is obsolete
			return PrefabUtility.GetPrefabType(go) == PrefabType.Prefab;
#pragma warning restore CS0618 // Type or member is obsolete
		}

		#endregion


		#region Set Editor Icon

		/// <summary>
		/// Set Editor Icon (the one that appear in SceneView)
		/// </summary>
		public static void SetEditorIcon(this GameObject gameObject, bool textIcon, int iconIndex) {
			GUIContent[] icons = textIcon ? GetTextures("sv_label_", string.Empty, 0, 8) : GetTextures("sv_icon_dot", "_pix16_gizmo", 0, 16);

			var egu = typeof(EditorGUIUtility);
			var flags = BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.NonPublic;
			var args = new object[] { gameObject, icons[iconIndex].image };
			var setIconMethod = egu.GetMethod("SetIconForObject", flags, null, new[] { typeof(Object), typeof(Texture2D) }, null);
			if (setIconMethod != null) setIconMethod.Invoke(null, args);
		}

		private static GUIContent[] GetTextures(string baseName, string postFix, int startIndex, int count) {
			GUIContent[] array = new GUIContent[count];
			for (int i = 0; i < count; i++) {
				array[i] = EditorGUIUtility.IconContent(baseName + (startIndex + i) + postFix);
			}

			return array;
		}

		#endregion


		#region Get Fields With Attribute

		/// <summary>
		/// Get all fields with specified attribute on all Components on scene
		/// </summary>
		public static ComponentField[] GetFieldsWithAttribute<T>() where T : Attribute {
			var allComponents = GetAllBehavioursInScenes();

			var fields = new List<ComponentField>();

			foreach (var component in allComponents) {
				if (component == null) continue;

				Type typeOfScript = component.GetType();
				var matchingFields = typeOfScript
					.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
					.Where(field => field.IsDefined(typeof(T), false));
				foreach (var matchingField in matchingFields) fields.Add(new ComponentField(matchingField, component));
			}

			return fields.ToArray();
		}

		public struct ComponentField {
			public readonly FieldInfo Field;
			public readonly Component Component;

			public ComponentField(FieldInfo field, Component component) {
				Field = field;
				Component = component;
			}
		}

		/// <summary>
		/// It's like FindObjectsOfType, but allows to get disabled objects
		/// </summary>
		/// <returns></returns>
		public static MonoBehaviour[] GetAllBehavioursInScenes() {
			var components = new List<MonoBehaviour>();

			for (var i = 0; i < SceneManager.sceneCount; i++) {
				var scene = SceneManager.GetSceneAt(i);
				if (!scene.isLoaded) continue;

				var root = scene.GetRootGameObjects();
				foreach (var gameObject in root) {
					var behaviours = gameObject.GetComponentsInChildren<MonoBehaviour>(true);
					foreach (var behaviour in behaviours) components.Add(behaviour);
				}
			}

			return components.ToArray();
		}

		#endregion


		#region Get Script Asseet Path

		/// <summary>
		/// Get relative to Assets folder path to script file location
		/// </summary>
		public static string GetRelativeScriptAssetsPath(ScriptableObject so) {
			MonoScript ms = MonoScript.FromScriptableObject(so);
			return AssetDatabase.GetAssetPath(ms);
		}

		/// <summary>
		/// Get full path to script file location
		/// </summary>
		public static string GetScriptAssetPath(ScriptableObject so) {
			var assetsPath = GetRelativeScriptAssetsPath(so);
			return new FileInfo(assetsPath).DirectoryName;
		}

		/// <summary>
		/// Get relative to Assets folder path to script file location
		/// </summary>
		public static string GetRelativeScriptAssetsPath(MonoBehaviour mb) {
			MonoScript ms = MonoScript.FromMonoBehaviour(mb);
			return AssetDatabase.GetAssetPath(ms);
		}

		/// <summary>
		/// Get full path to script file location
		/// </summary>
		public static string GetScriptAssetPath(MonoBehaviour mb) {
			var assetsPath = GetRelativeScriptAssetsPath(mb);
			return new FileInfo(assetsPath).DirectoryName;
		}

		#endregion


		public static void CopyToClipboard(string text) {
			TextEditor te = new TextEditor();
			te.text = text;
			te.SelectAll();
			te.Copy();
		}
	}
#endif
}