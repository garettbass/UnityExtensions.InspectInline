using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExtensions.InspectInlineExamples
{

    [CreateAssetMenu]
    public class RecursiveAsset : ScriptableObject
    {

        [InspectInline(targetIsSubasset = true)]
        public RecursiveAsset child;

    }

}