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

        [InspectInline(canCreateSubasset = true)]
        public SampleSubassetWithDoubleValue concreteSubasset;

        [InspectInline(canCreateSubasset = true)]
        public SampleSubasset polymorphicSubasset;

        [InspectInline(canCreateSubasset = true)]
        public ScriptableObject anyScriptableObject;

    }

}