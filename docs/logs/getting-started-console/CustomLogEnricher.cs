// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Diagnostics.Enrichment;

public class CustomLogEnricher : ILogEnricher
{
    public void Enrich(IEnrichmentTagCollector collector)
    {
        Console.WriteLine("Enriching log data with custom tag");
        // Add custom logic to enrich log data
        collector.Add("CustomTag", "CustomValue");
    }
}
