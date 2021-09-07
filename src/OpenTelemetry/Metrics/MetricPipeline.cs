// <copyright file="MeterProviderSdk.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    internal class MetricPipeline
    {
        internal const int MaxMetrics = 1000;
        private readonly Metric[] metrics;
        private readonly object collectLock = new object();
        private int metricIndex = -1;
        private AggregationTemporality temporality;
        private MetricReader metricReader;

        internal MetricPipeline(
            MetricReader metricReader)
        {
            this.metricReader = metricReader;
            this.temporality = metricReader.GetAggregationTemporality(); ;
        }

        internal void InstrumentPublished(Instrument instrument, MeterListener listener)
        {
            var index = Interlocked.Increment(ref this.metricIndex);
            if (index >= MaxMetrics)
            {
                // Log that all measurements are dropped from this instrument.
            }
            else
            {
                var metric = new Metric(instrument, this.temporality);
                this.metrics[index] = metric;
                listener.EnableMeasurementEvents(instrument, metric);
            }
        }

        internal void MeasurementsCompleted(Instrument instrument, object state)
        {
            Console.WriteLine($"Instrument {instrument.Meter.Name}:{instrument.Name} completed.");
        }

        internal void MeasurementRecordedDouble(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            // Get Instrument State
            var metric = state as Metric;

            if (instrument == null || metric == null)
            {
                // TODO: log
                return;
            }

            metric.UpdateDouble(value, tagsRos);
        }

        internal void MeasurementRecordedLong(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object>> tagsRos, object state)
        {
            // Get Instrument State
            var metric = state as Metric;

            if (instrument == null || metric == null)
            {
                // TODO: log
                return;
            }

            metric.UpdateLong(value, tagsRos);
        }

        internal IEnumerable<Metric> Collect()
        {
            lock (this.collectLock)
            {
                try
                {
                    // Record all observable instruments
                    this.listener.RecordObservableInstruments();
                    var indexSnapShot = Math.Min(this.metricIndex, MaxMetrics - 1);
                    for (int i = 0; i < indexSnapShot + 1; i++)
                    {
                        this.metrics[i].SnapShot();
                    }

                    return Iterate(this.metrics, indexSnapShot + 1);

                    // We cannot simply return the internal structure (array)
                    // as the user is not expected to navigate it.
                    // properly.
                    static IEnumerable<Metric> Iterate(Metric[] metrics, long targetCount)
                    {
                        for (int i = 0; i < targetCount; i++)
                        {
                            // Check if the Metric has valid
                            // entries and skip, if not.
                            yield return metrics[i];
                        }
                    }
                }
                catch (Exception)
                {
                    // TODO: Log
                    return default;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {

        }
    }
}
