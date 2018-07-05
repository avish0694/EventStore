using System;
using System.Collections.Generic;
using EventStore.Common.Log;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV4
{
    public class ptable_midpoint_calculations_should: SpecificationWithDirectory
    {
        protected byte _ptableVersion = PTableVersions.IndexV4;
        private static readonly ILogger Log = LogManager.GetLoggerFor<ptable_midpoint_calculations_should>();

        private void construct_same_midpoint_indexes_for_any_combination_of_params(int maxIndexEntries){
            for(var numIndexEntries = 0; numIndexEntries < maxIndexEntries;numIndexEntries++){
                for(var depth=0;depth<20;depth++){
                    var requiredMidpointsCount = PTable.GetRequiredMidpointCountCached(numIndexEntries,_ptableVersion, depth);
                    List<long> requiredMidpoints = new List<long>();
                    for(var k=0;k<requiredMidpointsCount;k++){
                        var index = PTable.GetMidpointIndex(k,numIndexEntries,requiredMidpointsCount);
                        requiredMidpoints.Add(index);
                    }

                    List<long> calculatedMidpoints = new List<long>();
                    for(var k=0;k<numIndexEntries;k++){
                        if(PTable.IsMidpointIndex(k,numIndexEntries,requiredMidpointsCount)){
                            calculatedMidpoints.Add(k);
                        }
                    }
                    if(numIndexEntries==1 && calculatedMidpoints.Count==1){
                        calculatedMidpoints.Add(calculatedMidpoints[0]);
                    }

                    if(requiredMidpoints.Count!=calculatedMidpoints.Count){
                        Log.Error("Midpoint count mismatch for numIndexEntries: {@numIndexEntries}, depth:{@depth} - Expected {@requiredMidpoints}, Found {@calculatedMidpoints}",numIndexEntries,depth,requiredMidpoints.Count,calculatedMidpoints.Count);
                    }
                    Assert.AreEqual(requiredMidpoints.Count,calculatedMidpoints.Count);

                    for(var i=0;i<requiredMidpoints.Count;i++){
                        if(requiredMidpoints[i]!=calculatedMidpoints[i]){
                            Log.Error("Midpoint mismatch at index {@i} for numIndexEntries: {@numIndexEntries}, depth:{@depth} - Expected {@fixthisvar}, Found {@fixthisvar}",i,numIndexEntries,depth,requiredMidpoints[i],calculatedMidpoints[i]); /*TODO: structured-log @shaan1337: the following parameters need attention: {3},{4}*/
                        }
                        Assert.AreEqual(requiredMidpoints[i],calculatedMidpoints[i]);
                    }
                }
            }
        }

        [Test, Category("LongRunning"), Ignore("Long running")]
        public void construct_same_midpoint_indexes_for_any_combination_of_params_large()
        {
            construct_same_midpoint_indexes_for_any_combination_of_params(4096);
        }

        [Test]
        public void construct_same_midpoint_indexes_for_any_combination_of_params_small()
        {
            construct_same_midpoint_indexes_for_any_combination_of_params(200);
        }
    }
}
