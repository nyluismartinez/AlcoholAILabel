Alcohol AI Label is a basic proof-of-concept application that uploads alcohol label images, runs OCR extraction with Tesseract, and uses a local AI model to convert raw OCR text into structured alcohol-label data.
Setup
1. Install Tesseract OCR
Download and install Tesseract OCR:
https://github.com/UB-Mannheim/tesseract/wiki

2. Install Ollama and Llama
Download Ollama:
https://ollama.com/download
Pull the Llama model:
ollama pull llama3.2
Start Ollama:
ollama serve
Verify the model:
ollama list
The application expects Ollama to run at:
http://localhost:11434

3. Setup the Database
Open a terminal in the folder containing:
AlcoholAILabel_API.csproj
Install EF tools if needed:
Add-migration
update-database

===========================================================================================
===========================================================================================

**What the Application Does**
The application starts with the API project and exposes endpoints for uploading alcohol label images, storing upload records, and sending uploaded labels into a processing pipeline, as follows:

  - User uploads label images
  - API Controller receives files
  - Records are saved in the database
  - User can upload many records, including 200+ labels
  - User clicks Send for Processing
  - API Controller read job to workers
  - Worker Service processes queued records
  - Tesseract OCR extracts raw text
  - AI model analyzes OCR text
  - Structured label data is saved


**Controller and Worker Pipeline**
The controller handles the upload process. At upload time, the system creates database records for each label image. The user can upload many records first before starting processing.
Processing does not need to happen immediately after each upload. The user can upload more than 200 records and then click Send for Processing.
After processing is triggered, the worker pipeline begins. The worker reads pending records, loads the uploaded image, runs Tesseract OCR, and sends the extracted text to the AI layer.

*NOTE: A small delay of 4 seconds has been added to some processing steps for demonstration purposes to view job status transitions and pipeline progress in the UI. This delay is implemented in the Worker Service and can be removed by commenting the delay value in the worker processing code. 
AlcoholAILabel_Worker -> AlcoholLabelJobProcessor.cs -> method ProcessNextJobAsync()
//await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);

**OCR and AI**
Tesseract OCR is responsible for reading text from the alcohol label image.
The AI model is responsible for understanding the raw OCR text and converting it into structured data such as:
Brand Name
Class / Type Designation
Alcohol Content
Net Contents
Producer / Bottler Name
Producer / Bottler Address
Country of Origin
Government Warning
Importer Name
Importer Address
Vintage Year
Appellation / Region
Lot Number
OCR extracts text. AI improves interpretation.

**Future Enhancements**
This pipeline could be enhanced to run on a server with GPU support for better AI performance.
Future versions could include:
GPU-enabled AI processing
More scalable queue architecture
Native OCR/image preprocessing
Better error handling and retry logic
Batch status tracking
Improved dashboard and reporting
Cloud or container deployment
Better confidence scoring

**Human review workflow**
The pipeline could also be improved with native C++ processing. Image cleanup, OCR preprocessing, and model inference can be optimized with native libraries.
Llama can also be used locally through Ollama, and more advanced native integration is possible using C++-based runtimes such as llama.cpp. This could improve performance and reduce dependency on external APIs.

**Enterprise Considerations**
This is a basic application created for a short take-home test.
For a more enterprise version, the project would need at least 6–8 weeks of development and 2 weeks of testing.
The current pipeline is automated, but the architecture would need to be improved to support better scalability, monitoring, reliability, error handling, security, and deployment.
Because the time for the take-home test was limited, this version focuses on demonstrating the main concept: upload labels, run OCR, use AI to extract structured data, and store the result.

**Human Review Note**
AI can help us work faster, but the final decision should always be made by a human.

