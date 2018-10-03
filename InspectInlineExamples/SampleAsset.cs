using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityExtensions.InspectInlineExamples
{

    // [CreateAssetMenu]
    public class SampleAsset : ScriptableObject
    {

        [InspectInline(canEditRemoteTarget = true)]
        public SampleSubasset remoteTarget;

        [InspectInline(targetIsSubasset = true)]
        public SampleSubassetWithVectorAndColorValues concreteSubasset;

        [InspectInline(targetIsSubasset = true)]
        public SampleSubasset polymorphicSubasset;

    }

}