using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityExtensions.InspectInlineExamples
{

    // [CreateAssetMenu]
    public class SampleSubassetWithEnumValue : SampleSubasset
    {

        public enum Enum
        {
            Foo,
            Bar,
            Baz,
        }

        public Enum enumValue;

    }

}