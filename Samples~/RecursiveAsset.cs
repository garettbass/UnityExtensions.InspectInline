using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExtensions.InspectInlineExamples
{

    // [CreateAssetMenu]
    public class RecursiveAsset : ScriptableObject
    {

        [InspectInline(canCreateSubasset = true)]
        public RecursiveAsset child;

    }

}