# CARE DICOM Enabler - Setup Guide

## Prerequisites

* Visual Studio installed
* Administrator access on the machine
* Valid CARE API credentials and configuration values

## Installation and Configuration

### Step 1: Open Visual Studio

Launch **Visual Studio** using **Run as Administrator**.

### Step 2: Open the Solution

Open the **CARE DICOM Enabler** solution in Visual Studio.

### Step 3: Configure CARE API Settings

Navigate to the **App Config** page in the `CARE_MWL_Service` project.

Update the following configuration values:

* `careBaseUrl`
* `careToken`
* `careModality`

Ensure all values are valid for the target environment before proceeding.

### Step 4: Run the Application

1. Build and run the application.
2. Log in to the application.
3. Install the DICOM Enabler services.
4. Start the DICOM Enabler services.

## Verification

After the services are started successfully:

* Verify that all DICOM Enabler services are running.
* Confirm connectivity with the configured CARE API endpoint.
* Validate that the configured modality is functioning as expected.
