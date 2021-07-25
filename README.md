# Cloud Infrastructure as .NET Code with Pulumi

A presentation for Hainton .NET 27th July 2021

## Overview

This is - or should be - the content for a presentation on Pulumi.

So there will probably be:

* A powerpoint file, or equivalent
* Source for code that's going to be deployed by the demonstrations. 
  * An azure function, an aws lambda (both slightly dated)
* Source for the Pulumi stacks (probably)
* Notes to myself

The repo will probably be a bit odd in places in order to allow for some live coding i.e. complete code may be removed and replaced by incomplete code - so look at links and tags

## Folders

This pretty much all lives off the root - its not meant to be perfect

* `aws-lambda` - source for the AWS Lambda wrapper round the quote fetch function
* `azure-func` - source for the Azure Function wrapper round the quote fetch function
* `QuoteFetch` - the common code to do the fun stuff reading from a remote source and interacting with a blob store (Azure blobs or S3 buckets)
* `pulumi` - where the pulumi code lives
