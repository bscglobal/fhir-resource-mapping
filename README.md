# .NET FHIR Resource Mapper

NOTE: This library is still under development. 

## Overview

This is an implementation of resource mapping parts of the FHIR [Structured Data Capture](https://hl7.org/fhir/uv/sdc/index.html) specification, ([Data Extraction](https://hl7.org/fhir/uv/sdc/extraction.html) and [Automatic Population](https://hl7.org/fhir/uv/sdc/populate.html)), using types supplied from the [Firely SDK](https://github.com/FirelyTeam/firely-net-sdk).

The implementation is heavily based upon the Android FHIR SDC [implementation](https://github.com/google/android-fhir/blob/master/datacapture).

## Planned Capabilities

### Population

The population functionality should follow the FHIR SDC [specification](https://hl7.org/fhir/uv/sdc/populate.html), but at this point only [expression-based](https://hl7.org/fhir/uv/sdc/populate.html#expression-based-population) population is implemented.

The library should be able to populate `QuestionnareResponse` based on a given `Questionnaire` and a list of required resources. A possible future feature will be to parse a `Questionnaire` and return a list of required resources (not the resources themselves but the details of how to fetch them, the library won't do any calls to a FHIR server itself).

### Extraction

The extraction functionality should follow the FHIR SDC [specification](https://hl7.org/fhir/uv/sdc/extract.html), but at this point only [definition-based](https://hl7.org/fhir/uv/sdc/extraction.html#definition-based-extraction) extraction is implemented.

The library should be able to extract FHIR resources from a `QuestionnareResponse` based on a given `Questionnaire`.

## Installation

```
dotnet add package BSC.Fhir.ResourceMapping
```
