// Copyright (c) 2016 Perfare - https://github.com/Perfare/Il2CppDumper/
// Copyright (c) 2016 Alican Çubukçuoğlu - https://github.com/AlicanC/AlicanC-s-Modern-Warfare-2-Tool/
// Copyright (c) 2017-2021 Katy Coe - http://www.djkaty.com - https://github.com/djkaty/Bin2Object/

using System;

namespace NoisyCowStudios.Bin2Object
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ArrayLengthAttribute : Attribute
    {
        public string FieldName { get; set; }
        public int FixedSize { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class StringAttribute : Attribute
    {
        public bool IsNullTerminated { get; set; }
        public int FixedSize { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class VersionAttribute : Attribute
    {
        public double Min { get; set; } = -1;
        public double Max { get; set; } = -1;
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class SkipWhenReadingAttribute : Attribute { }
}
