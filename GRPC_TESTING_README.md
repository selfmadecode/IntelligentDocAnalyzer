# Testing the gRPC Recognition Service

This guide explains how to test the `RecognitionGrpcService` using various tools including Postman, grpcurl, and grpcui.

## Overview

The gRPC Recognition Service provides three main endpoints:

1. **SubmitFile** - Upload and queue a bank statement file for recognition
2. **GetResult** - Poll the status and results of a previously submitted job
3. **AnalyzeUrl** - Analyze a bank statement directly from a URL

All three methods make calls to the Azure Document Intelligence service, just like the REST API does.

## Prerequisites

- The application must be running (typically on `localhost:7290`)
- gRPC Reflection must be enabled (enabled in Development environment by default)
- For Postman: Postman v9.22.0 or later with gRPC support

## Method 1: Using Postman (Recommended for GUI)

### Setup

1. **Open Postman** and create a new gRPC request
2. **Enter the server URL**: `localhost:7290` (or your application's port)
3. **Enable gRPC Reflection**: Postman will automatically discover the service methods

### SubmitFile - Upload a File

1. **Method**: Click the gRPC method dropdown and select:
   - Service: `recognition.Recognition`
   - Method: `SubmitFile`

2. **Prepare the request**:
   ```json
   {
     "file_name": "statement.pdf",
     "file_content": "<base64-encoded-file-content>"
   }
   ```

3. **To encode a file to base64**:
   - Use any online base64 encoder or:
   - In PowerShell: `[Convert]::ToBase64String([IO.File]::ReadAllBytes('C:\path\to\statement.pdf'))`
   - In Linux: `base64 -w 0 statement.pdf`

4. **Send the request** and you'll receive:
   ```json
   {
     "job_id": "550e8400-e29b-41d4-a716-446655440000",
     "status": "Queued"
   }
   ```

5. **Save the job_id** - you'll need it for the GetResult call

### GetResult - Check Job Status

1. **Method**: Select:
   - Service: `recognition.Recognition`
   - Method: `GetResult`

2. **Prepare the request**:
   ```json
   {
     "job_id": "550e8400-e29b-41d4-a716-446655440000"
   }
   ```

3. **Send the request** and you'll receive:
   ```json
   {
     "job_id": "550e8400-e29b-41d4-a716-446655440000",
     "status": "Completed",
     "file_name": "statement.pdf",
     "model_used": "prebuilt-bankStatement.us",
     "error_message": "",
     "result_json": "{\"transactions\": [...], \"analysis\": [...]}"
   }
   ```

   - **Status values**: `Queued`, `Processing`, `Completed`, `Failed`
   - When `status` is `Completed`, `result_json` contains the full analysis result

### AnalyzeUrl - Direct URL Analysis

1. **Method**: Select:
   - Service: `recognition.Recognition`
   - Method: `AnalyzeUrl`

2. **Prepare the request**:
   ```json
   {
     "file_url": "https://example.com/statement.pdf"
   }
   ```

3. **Send the request** and you'll receive (immediately):
   ```json
   {
     "result_json": "{\"transactions\": [...], \"analysis\": [...]}"
   }
   ```

## Method 2: Using grpcurl (Command Line)

### Installation

```bash
# macOS
brew install grpcurl

# Linux
go install github.com/fullstorydev/grpcurl/cmd/grpcurl@latest

# Windows (via Chocolatey)
choco install grpcurl

# Or download from: https://github.com/fullstorydev/grpcurl/releases
```

### List Available Services

```bash
grpcurl -plaintext localhost:7000 list
```

Output:
```
grpc.reflection.v1.ServerReflection
grpc.reflection.v1alpha.ServerReflection
recognition.Recognition
```

### SubmitFile Request

```bash
# Create a request file (submit.json)
cat > submit.json << 'EOF'
{
  "file_name": "statement.pdf",
  "file_content": "JVBERi0xLjQK..."
}
EOF

# Send the request
grpcurl -plaintext -d @ localhost:7000 recognition.Recognition/SubmitFile < submit.json
```

**Response**:
```json
{
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Queued"
}
```

### GetResult Request

```bash
# Create a request file (get_result.json)
cat > get_result.json << 'EOF'
{
  "job_id": "550e8400-e29b-41d4-a716-446655440000"
}
EOF

# Send the request
grpcurl -plaintext -d @ localhost:7000 recognition.Recognition/GetResult < get_result.json
```

### AnalyzeUrl Request

```bash
# Create a request file (analyze_url.json)
cat > analyze_url.json << 'EOF'
{
  "file_url": "https://example.com/statement.pdf"
}
EOF

# Send the request
grpcurl -plaintext -d @ localhost:7000 recognition.Recognition/AnalyzeUrl < analyze_url.json
```

### View Service Definition

```bash
grpcurl -plaintext localhost:7000 describe recognition.Recognition
```

## Method 3: Using grpcui (Web Interface)

### Installation

```bash
go install github.com/fullstorydev/grpcui/cmd/grpcui@latest
```

### Launch Web Interface

```bash
grpcui -plaintext -open localhost:7000
```

This opens a browser-based gRPC testing interface similar to Swagger/OpenAPI for REST APIs.

## Testing Workflow

### Complete End-to-End Test

1. **Submit a file**:
   - Call `SubmitFile` with your PDF file
   - Note the returned `job_id`

2. **Poll for results** (immediately after):
   - Call `GetResult` with the `job_id`
   - Status will likely be `Queued` or `Processing`

3. **Wait and poll again**:
   - After a few seconds, call `GetResult` again
   - When status is `Completed`, `result_json` will contain the analysis

4. **Parse the results**:
   - The `result_json` field contains a JSON-serialized `StatementAnalysisResult` with:
     - `transactions`: Extracted financial transactions
     - `analysis`: Statistical analysis of the statement
     - `model_used`: Which Azure model was used

### Direct URL Analysis

1. **Call `AnalyzeUrl`** with a publicly accessible PDF URL
2. **Receive immediate results** (blocking call, waits for Azure to complete)
3. **Check the `result_json`** for the analysis

## Error Handling

### Common Errors and Solutions

| Error | Cause | Solution |
|-------|-------|----------|
| `Connection refused` | App not running | Start the application |
| `failed to dial "localhost:7000": connection refused` | Wrong port | Check `appsettings.json` for gRPC port |
| `InvalidArgument: file_name and file_content are required` | Missing or empty fields | Ensure both fields are provided and not empty |
| `InvalidArgument: job_id is not a valid GUID` | Invalid job ID format | Use the exact UUID from SubmitFile response |
| `NotFound: Job X was not found` | Job ID doesn't exist | Verify the job ID is correct |
| `Unauthenticated: Azure credentials are invalid or not configured` | Missing Azure credentials | Check environment variables: `AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT` and `AZURE_DOCUMENT_INTELLIGENCE_KEY` |
| `InvalidArgument: Invalid fileUrl provided` | Malformed URL | Ensure URL is valid and accessible |

## Example Test Cases

### Test Case 1: Upload and Poll

```bash
# Step 1: Upload file
grpcurl -plaintext -d '{"file_name": "test.pdf", "file_content": "JVBERi0xLjQK..."}' \
  localhost:7000 recognition.Recognition/SubmitFile

# Output: {"jobId":"abc-123","status":"Queued"}

# Step 2: Poll after 5 seconds
grpcurl -plaintext -d '{"job_id": "abc-123"}' \
  localhost:7000 recognition.Recognition/GetResult

# Output may show: {"jobId":"abc-123","status":"Processing",...}
```

### Test Case 2: Direct URL Analysis (No Polling Needed)

```bash
grpcurl -plaintext \
  -d '{"file_url": "https://example.com/bank-statement.pdf"}' \
  localhost:7000 recognition.Recognition/AnalyzeUrl

# Output: {"resultJson": "{\"transactions\":[...]}"}
```

## Parsing gRPC Responses in Postman

The `result_json` field contains a JSON string. To parse it:

1. In the response, look for the `result_json` field
2. Copy the value (which is a stringified JSON)
3. Use an online JSON formatter or parse it programmatically

Example response structure:
```json
{
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Completed",
  "fileName": "statement.pdf",
  "modelUsed": "prebuilt-bankStatement.us",
  "errorMessage": "",
  "resultJson": "{\"transactions\":[{\"date\":\"2024-01-15\",\"description\":\"Deposit\",\"amount\":1500.00}],\"summary\":{\"totalDeposits\":5000.00,\"totalWithdrawals\":2000.00}}"
}
```

Parse the `resultJson` to get:
```json
{
  "transactions": [
    {
      "date": "2024-01-15",
      "description": "Deposit",
      "amount": 1500.00
    }
  ],
  "summary": {
    "totalDeposits": 5000.00,
    "totalWithdrawals": 2000.00
  }
}
```

## Key Differences from REST API

| Aspect | gRPC | REST |
|--------|------|------|
| Protocol | Binary (HTTP/2) | Text (HTTP/1.1) |
| Method | SubmitFile | POST /api/recognition/analyze-file |
| Method | GetResult | GET /api/recognition/{id} |
| Method | AnalyzeUrl | POST /api/recognition/analyze-url |
| Response Time | Faster (binary) | Slightly slower (JSON parsing) |
| File Encoding | Base64 in protobuf | Multipart form data |
| Job Polling | Required for file upload | Required for file upload |
| Direct URL | Available (AnalyzeUrl) | Available (analyze-url) |

## Troubleshooting

### Service Discovery Issues

If Postman can't discover the service:

1. **Enable gRPC Reflection** in `Program.cs`:
   ```csharp
   if (app.Environment.IsDevelopment())
   {
       app.MapGrpcReflectionService();
   }
   ```

2. **Verify gRPC endpoint** is running:
   ```bash
   grpcurl -plaintext localhost:7000 list
   ```

3. **Check application logs** for startup errors

### File Upload Issues

- Ensure file content is properly base64 encoded
- File must be a valid PDF or supported document format
- File must not exceed 25MB (configured in `RequestSizeLimit`)

### Azure Service Issues

- Verify Azure credentials are set:
  ```bash
  $env:AZURE_DOCUMENT_INTELLIGENCE_ENDPOINT = "https://your-resource.cognitiveservices.azure.com/"
  $env:AZURE_DOCUMENT_INTELLIGENCE_KEY = "your-key"
  ```

- Test Azure connectivity:
  ```bash
  grpcurl -plaintext \
    -d '{"file_url": "https://example.com/test.pdf"}' \
    localhost:7000 recognition.Recognition/AnalyzeUrl
  ```

## Performance Notes

- **SubmitFile + GetResult**: Asynchronous, files are queued in MongoDB and processed by Hangfire background jobs
- **AnalyzeUrl**: Synchronous, blocking call that waits for Azure to complete
- **Typical processing time**: 2-10 seconds depending on file complexity and Azure load

## Security Considerations

For production deployments:

1. **Disable gRPC Reflection** in production (currently only enabled in Development)
2. **Use TLS encryption** instead of plaintext connections
3. **Implement authentication** (JWT, mTLS, etc.)
4. **Rate limit** gRPC endpoints
5. **Validate** all input file URLs and formats

## Additional Resources

- [gRPC Documentation](https://grpc.io/docs/)
- [Postman gRPC Support](https://learning.postman.com/docs/sending-requests/grpc/grpc-request-interface/)
- [grpcurl GitHub](https://github.com/fullstorydev/grpcurl)
- [grpcui GitHub](https://github.com/fullstorydev/grpcui)
- [Protocol Buffers Documentation](https://developers.google.com/protocol-buffers)
