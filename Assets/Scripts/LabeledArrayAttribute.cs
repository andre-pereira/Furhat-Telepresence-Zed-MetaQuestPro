using UnityEngine;
using System;

public class LabeledArrayAttribute : PropertyAttribute
{
    public readonly string[] names;
    public LabeledArrayAttribute(string[] names) { this.names = names; }
    public LabeledArrayAttribute(Type enumType) { names = Enum.GetNames(enumType); }
}
