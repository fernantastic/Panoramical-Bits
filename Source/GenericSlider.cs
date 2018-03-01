using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

public class GenericSlider : BaseSlider {
	
	public enum ChangeType {
		ValueSystem,
		Koreographer,
		ManualValue,
		LFO
	}

	[SerializeField]
	public GameObject targetObject;

	// Deprecated
	public string targetComponent = "";
	// Deprecated
	public string targetProperty = "";

	public Component singleTargetComponent;
	public string singleTargetProperty = "";
	public string singleTargetSubProperty = "";
	public string componentType = ""; // to debug components that were erased
	
	public ChangeType changeBy = ChangeType.ValueSystem;
	
	public ValueSystem.ValueType value;
	public AnimationCurve curve = AnimationCurve.Linear(0,0,1,1);
	public float rangeMin = 0;
	public float rangeMax = 1;

	public bool useKoreographer = false; // deprecated
	public Koreography koreography;
	public KoreographyTrack koreographerTrack;
	public float koreographerOneOffReturnSpeed = 0.5f;
	public bool koreographerListenToTime = false;
	
	public float lfoBaseValue = 0;
	public float lfoFrequency = 0.25f;
	public float lfoAmplitude = 1;
	public bool lfoRandomSeed = false;
	public AnimationCurve lfoShape = new AnimationCurve(new Keyframe[3]{new Keyframe(0,1,0,1), new Keyframe(0.5f,-1,-1,1), new Keyframe(1,1,-1,1)});

	public float manualValue = 0; //useful to animate properties while keeping the good stuff about this
	
	public Gradient colorValueGradient = new Gradient ();
	
	public float multiplier = 1;
	public ValueSystem.ValueType multiplyByValue;
	public AnimationCurve multiplyByCurve = AnimationCurve.Linear(0,0,1,1);

	// Private
	private Component _component;
	private string _property;
	private string _subproperty;
	
	float _lfoSeed = 0;
	
	private const BindingFlags bflags = BindingFlags.Public | BindingFlags.Instance;

	protected override void Awake ()
	{
		base.Awake();
		if (targetObject == null) targetObject = gameObject;

		_component = singleTargetComponent;
		if (targetComponent.Length > 0)
			_component = targetObject.GetComponent(targetComponent);

		if (_component == null) {
			Debug.LogWarning("Cannot find component '" + targetComponent + "', check it's written exactly as the script's name", transform);
			enabled = false;
			return;
		}

		if (targetProperty.Length > 0) {
			_property = targetProperty;

			if (_property.Split('.').Length > 1) {
				string[] properties = _property.Split('.');
				_property = properties[properties.Length-2];
				_subproperty = properties[properties.Length-1];
			}
		} else {
			_property = singleTargetProperty;
			_subproperty = singleTargetSubProperty;
		}
		
		if (changeBy == ChangeType.LFO && lfoRandomSeed)
			_lfoSeed = Random.value;

		//Debug.Log("Target component " + _component);
		if (GetValue() == null) {
			Debug.LogWarning("Cannot find property '" + _property + "', check it's written exactly as in the script", transform);
			enabled = false;
			return;
		}
	}
	protected override void Start ()
	{
		base.Start ();
		
		if (singleTargetComponent && singleTargetComponent.GetType() == typeof(AudioSource) && singleTargetProperty == "volume") {
			Debug.LogError("Can't change volume with a slider directly! Add a SoundVolume script instead.", this);
			enabled = false;
			return;
		}
		
		//Debug.Log("///// target component = " + targetComponent + ", target property " + targetProperty + " = " + GetValue(targetComponent, targetProperty));
		//Debug.Log("is float? " + (GetValue().GetType() == typeof(float)));
		if ((useKoreographer || changeBy == ChangeType.Koreographer) && koreography && koreographerTrack && Koreographer.Instance) {
			if (koreographerListenToTime)
				Koreographer.Instance.RegisterForEventsWithTime(koreographerTrack.EventID, OnKoreographerTime);
			else
				Koreographer.Instance.RegisterForEvents(koreographerTrack.EventID, OnKoreographerOneOff);
		}
		
		if (changeBy == ChangeType.ValueSystem) {
			if (value != ValueSystem.ValueType.Unassigned) {
				ValueSystem.instance.RegisterCallback(value, this);
				if (multiplyByValue != ValueSystem.ValueType.Unassigned) {
					ValueSystem.instance.RegisterCallback(multiplyByValue, this);
				}
			}
		}
	}


	void LateUpdate ()
	{
		//object v = GetValue();
		if ((useKoreographer || changeBy == ChangeType.Koreographer) && koreography != null && koreographerTrack != null && koreographerOneOffReturnSpeed != 0) {
			SetValue(iTween.FloatUpdate((float)GetFloatValue(), rangeMin, koreographerOneOffReturnSpeed));
			//SetValue(Mathf.Lerp((float)GetFloatValue(), rangeMin, koreographerOneOffReturnSpeed));
		}
		if (changeBy == ChangeType.LFO) {
			float lfoValue = lfoBaseValue + lfoShape.Evaluate(Mathf.Repeat(Time.time * lfoFrequency + _lfoSeed,1)) * lfoAmplitude;
			SetValue(lfoValue);
		} else if ((changeBy == ChangeType.ValueSystem && value == ValueSystem.ValueType.Unassigned) || changeBy == ChangeType.ManualValue) {
			SetValue(GetMinMaxValue(rangeMin,rangeMax,curve.Evaluate(manualValue)));
		}
		
	}

	protected override void OnDestroy() {
		if ((useKoreographer  || changeBy == ChangeType.Koreographer) && koreography && koreographerTrack && Koreographer.Instance) {
			Koreographer.Instance.UnregisterForAllEvents(this);
			/*
			if (koreographerListenToTime)
				Koreographer.Instance.UnregisterForEvents(koreographerTrack.EventID, OnKoreographerTime);
			else
				Koreographer.Instance.UnregisterForEvents(koreographerTrack.EventID, OnKoreographerOneOff);
				*/
		}
		if (ValueSystem.instance)
			ValueSystem.instance.UnRegisterCallbacks(this);
	}

	void OnEnable() {
		//OnValueChange(value);
	}

	public override void OnValueChange (ValueSystem.ValueType Type)
	{
		if (!isActiveAndEnabled)
			return;
		
		base.OnValueChange (Type);
		if(Type == value) SetValue(GetMinMaxValue(rangeMin, rangeMax, ValueSystem.instance.GetValue(value) * multiplier, curve));
		if (Type == multiplyByValue) {
			multiplier = multiplyByCurve.Evaluate(ValueSystem.instance.GetValue (Type));
			OnValueChange(value);
		}

	}

	void OnKoreographerOneOff(KoreographyEvent kevent) {
		SetValue(rangeMax);

	}
	void OnKoreographerTime(KoreographyEvent kevent, int sampleTime, int sampleDelta) {
		if (kevent.Payload != null && kevent.Payload as CurvePayload != null) {
			SetValue(GetMinMaxValue (rangeMin, rangeMax, (kevent.Payload as CurvePayload).GetValueAtDelta(kevent.GetEventDeltaAtSampleTime(sampleTime))));
		}
	}
	

	public object GetValue() {
		object propertyValue = GetValue(_component, _property);
		if (_subproperty != null && _subproperty.Length > 0) {
			object v = GetValue(propertyValue, _subproperty);
			if (v != null) {
				return v;
			}
			Debug.LogWarning("Cannot find subproperty " + _property + "." + _subproperty, this);
		}
		return propertyValue;
	}

	float GetFloatValue() {
		object val = GetValue();
		if (val.GetType() == typeof(Vector3))
			return (val as Vector3?).GetValueOrDefault().x;
		return (float)val;
	}

	public static void SetValue(object component, string property, object value, string subproperty = null)
	{
		object target = GetValue (component, property);
		System.Type targetType = target.GetType ();
		
		if (targetType == typeof(Vector2)) {
			Vector2 v;
			if (subproperty == null || subproperty.Length == 0) {
				v = (component as Vector2?).GetValueOrDefault();
				v.x = v.y = (float)value;
			} else {
				v = (Vector2)target;
				if (subproperty == "x") v.x = (float)value;
				if (subproperty == "y") v.y = (float)value;
			}
			value = (object)v;
		}
		if (targetType == typeof(Vector3)) {
			Vector3 v;
			if (subproperty == null || subproperty.Length == 0) {
				v = (component as Vector3?).GetValueOrDefault();
				v.x = v.y = v.z = (float)value;
			} else {
				v = (Vector3)target;
				if (subproperty == "x") v.x = (float)value;
				if (subproperty == "y") v.y = (float)value;
				if (subproperty == "z") v.z = (float)value;
			}
			value = (object)v;
		}
		if (targetType == typeof(Vector4)) {
			Vector4 v;
			if (subproperty == null || subproperty.Length == 0) {
				v = (component as Vector4?).GetValueOrDefault();
				v.x = v.y = v.z = v.w = (float)value;
			} else {
				v = (Vector4)target;
				if (subproperty == "x") v.x = (float)value;
				if (subproperty == "y") v.y = (float)value;
				if (subproperty == "z") v.z = (float)value;
				if (subproperty == "w") v.w = (float)value;
			}
			value = (object)v;
		}
		
		
		if (targetType == typeof(int)) {
			value = System.Convert.ToInt32(value);
		}
		
		if (targetType == typeof(Color)) {
			Color c = (Color)target;
			if (subproperty == null || subproperty.Length == 0) {
				c = (Color)value;
			} else {
				if (subproperty == "r") c.r = (float)value;
				if (subproperty == "g") c.g = (float)value;
				if (subproperty == "b") c.b = (float)value;
				if (subproperty == "a") c.a = (float)value;
			}
			value = (object)c;
		}
		
		System.Type componentType = component.GetType();
		PropertyInfo propertyInfo = componentType.GetProperty(property, bflags);
		if (propertyInfo != null) {
			propertyInfo.SetValue(component, value, null);
			return;
		}
		FieldInfo fieldInfo = componentType.GetField(property, bflags);
		if (fieldInfo != null) {
			fieldInfo.SetValue(component, value);
		}
	}
	public virtual void SetValue( object Value ) {
		if (GetValue ().GetType () == typeof(Color) && Value.GetType() == typeof(float)) {
			Value = colorValueGradient.Evaluate((float)Value);
		}
		SetValue(_component, _property, Value, _subproperty != null && _subproperty.Length > 0 ? _subproperty : null);
	}

	#region Public
	public void OnPropertiesChanged() {
		ValueSystem.instance.UnRegisterCallbacks(this);
		_component = singleTargetComponent;
		_property = singleTargetProperty;
		_subproperty = singleTargetSubProperty;
		if (_component != null && _property.Length > 0) {
			ValueSystem.instance.RegisterCallback(value, this);
			if (multiplyByValue != ValueSystem.ValueType.Unassigned) {
				ValueSystem.instance.RegisterCallback(multiplyByValue, this);
			}
			enabled = true;
		} else {
			enabled = false;
		}
	}
	#endregion

	#region static
	public static List<string> GetProperties(object source)
	{
		List<string> props = new List<string>();
		if (source != null) {
			foreach (PropertyInfo pi in source.GetType().GetProperties(bflags)) {
				if(GenericSlider.isTypeSupported(pi.PropertyType) && pi.CanWrite && !pi.GetSetMethod().IsStatic && !pi.IsDefined(typeof(System.ObsoleteAttribute), true))
					props.Add(pi.Name);
			}
			foreach( FieldInfo fi in source.GetType().GetFields(bflags)) {
				if(GenericSlider.isTypeSupported(fi.FieldType) && fi.IsPublic && !fi.IsLiteral && !fi.IsStatic)
					props.Add(fi.Name);
			}

		}
		return props;
	}
	public static object GetValue(object source, string property)
	{
		object val = null;
		if (source != null && property != null) {
			System.Type sourceType = source.GetType();
			PropertyInfo propertyInfo = sourceType.GetProperty(property, bflags);
			if (propertyInfo != null)
				return propertyInfo.GetValue(source, null);
			FieldInfo fieldInfo = sourceType.GetField(property, bflags);
			if (fieldInfo != null)
				return fieldInfo.GetValue(source);
		}
		return val;
	}
	public static bool isTypeSupported(System.Type PropertyType) {
		System.Type[] supportedTypes = {
			typeof(Vector2),
			typeof(Vector3),
			typeof(Vector4),
			typeof(float),
			typeof(double),
			typeof(int),
			typeof(Color)
		};
		return supportedTypes.Contains(PropertyType);
	}
	#endregion


}
