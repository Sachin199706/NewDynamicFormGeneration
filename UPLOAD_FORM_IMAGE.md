# Upload Form Image

Create a new form from an image of a form (a scan, a photo or a screenshot).
The fields are found automatically, the user checks them, and a new **Draft** form is created
and opened in the Form Builder.

Everything runs **locally**: the image never leaves the browser, and the AI model runs in your own Ollama.

---

## How it works

```
Browser (Angular)                                         API (.NET)                Ollama (Docker)
─────────────────                                         ──────────                ───────────────
1. Check the file (PNG / JPG, max 5 MB)
2. OCR with Tesseract.js → text lines + word positions
3. Fixed rules in TypeScript:
     first line = form title
     "*" = required
     O / QO / © marks  = Radio options
     [] / [J / OJ marks = tick-box options (Checkbox / CheckboxList)
     "No file chosen" / "Choose File" = File upload
     grey / unreadable lines and button text are dropped
4. Labels with no type yet ──────── POST classify-labels ──► small check ──► llama3.2 chooses:
                                                             one request    TextBox, Number, Date,
5. Apply the AI types  ◄─────────── one type per label ◄──── at a time      Dropdown, File, Label, Ignore
6. Rows and column spans from the label positions
   (fields side by side in the image → same row, span 1–12)
7. User reviews and edits the fields
8. Create Form → POST api/forms + POST api/forms/versions (existing APIs) → Form Builder
```

Why not let the AI do everything? It was tested: with the whole OCR text, `llama3.2` (3B) got
5 of 11 fields right. With the rules in code and the AI only choosing types, it got 11 of 11
and 15 of 15 on the sample forms.

---

## Setup

### 1. Ollama and the model

Ollama runs in the `ollama` Docker container on port `11434`. The model must be installed in it:

```
ollama pull llama3.2
ollama list          # must show llama3.2:latest
```

`llama3.2` is the 3B text model (about 2 GB). It runs on CPU; one analysis takes about 1–2 minutes
on a computer without an NVIDIA graphics card.

### 2. API settings (`NewDynamicFormGenAPI/appsettings.json`)

```json
"Ollama": {
  "BaseUrl": "http://localhost:11434",
  "ModelName": "llama3.2",
  "TimeoutSeconds": 300,
  "MaxFormTextLength": 20000,
  "MaxRequestsPerMinute": 10
}
```

| Setting | Meaning |
|---|---|
| `BaseUrl` | Address of the Ollama server |
| `ModelName` | Ollama model used to choose field types. Change it here to try another model. |
| `TimeoutSeconds` | How long the API waits for the model before giving up |
| `MaxFormTextLength` | Maximum total characters of labels in one request |
| `MaxRequestsPerMinute` | Rate limit of the AI endpoint (the API has no login) |

### 3. Angular app

```
cd DynamicFormGenerationUI
npm install
npm start
```

`npm install` adds `tesseract.js` and its English data (`@tesseract.js-data/eng`).
`angular.json` copies the OCR files into the app under `/tesseract/` (about 15 MB), so OCR works
offline. They are only downloaded by the browser when the Upload Form Image page is used.

---

## Using it

1. Click the **Upload Form Image** icon in the left bar.
2. Drop or choose an image of a form, then click **Analyze Image**.
3. Check Form Name and Form Code (filled from the image title).
4. Check the fields: label, type, required, options. Rows with a problem are highlighted.
   "Found by" shows whether the type came from the **Rules**, the **AI** or **You**.
5. Click **Create Form**. The new Draft form opens in the Form Builder, where it can be adjusted,
   saved and published as usual.

If the AI is not running, the page still works: fields without a clear type become TextBox and a
warning asks the user to check them.

---

## API endpoints

| Endpoint | Description |
|---|---|
| `GET api/form-image/status` | `{ isAvailable, modelName }`: is Ollama running and the model installed |
| `POST api/form-image/classify-labels` | Body `{ "labels": ["Employee Name", "Leave Type"] }` → one type per label. Rate limited; only one request runs at a time (others get "The AI is busy"). |

Both return the usual `{ success, message, data, errors }` shape. Test requests are in
`NewDynamicFormGenAPI/NewDynamicFormGenAPI.http`.

---

## Tests

```
cd DynamicFormGenerationUI
npx ng test --watch=false
```

- `core/services/form-image-mapper.spec.ts`: the rules, AI types, warnings, controls and layout,
  using real OCR results of two sample forms
- `features/upload-form-image/upload-form-image.spec.ts`: the page flow (analyze, AI not available,
  AI error, no text, create form, errors)

---

## Known limits

- **English, printed forms.** Handwriting, blurry, rotated or dark photos give poor results.
- **One page per image.** Crop away anything that is not the form.
- OCR cannot see shapes. Radio buttons and tick boxes are found from the marks OCR reads (`O`, `[]`);
  dropdown options are usually not visible in an image, so the user adds them.
- Layout supports rows and column span. Row span and sections are not created.
- About 1–2 minutes per analysis on CPU. With an NVIDIA GPU it is much faster.

---

## Troubleshooting

| Problem | What to check |
|---|---|
| Badge shows "AI not available" | Is the `ollama` container running? Does `ollama list` show `llama3.2`? Is `BaseUrl` correct? |
| "The AI service did not answer within … seconds" | The PC is busy or slow. Try again, or raise `TimeoutSeconds`. |
| "The AI is busy with another form" | Only one analysis runs at a time. Wait for it to finish. |
| "Too many AI requests" | Rate limit reached. Wait one minute. |
| "No readable text was found" | Use a clearer, straight image of a printed form. |
| OCR never finishes / 404 on `/tesseract/...` | Run `npm install` again and restart `npm start`, so the OCR files are copied. |
| "The Form Code may already be in use" | Form codes are unique. Choose another code. |

---

## Files

**API (`NewDynamicFormGenAPI`)**

| File | Purpose |
|---|---|
| `Controllers/FormImageController.cs` | `status` and `classify-labels` endpoints |
| `Models/Services/FormImageAnalysisService.cs` | Input check, one request at a time, prompt, reads the answer |
| `Models/Services/OllamaClient.cs` | HTTP calls to Ollama |
| `Models/Interfaces/IFormImageAnalysisService.cs`, `IOllamaClient.cs` | Contracts |
| `Models/DTOs/FormImage/FormImageDtos.cs`, `Models/DTOs/Ollama/OllamaDtos.cs` | Request and response classes |
| `Models/Common/OllamaOptions.cs`, `OllamaServiceException.cs` | Settings and error type |
| `Program.cs`, `appsettings.json` | Registration, rate limit, settings |

**Angular (`DynamicFormGenerationUI/src/app`)**

| File | Purpose |
|---|---|
| `features/upload-form-image/` | The page |
| `core/services/form-image-ocr.ts` | OCR in the browser |
| `core/services/form-image-mapper.ts` | Rules, AI types, checks, controls and layout |
| `core/services/form-image.ts` | API calls |
| `core/models/form-image.model.ts` | Types |
| `core/interceptors/loading-interceptor.ts` | `SKIP_GLOBAL_LOADER` so the long AI call shows its own progress |
| `core/interceptors/error-interceptor.ts` | Fixed: HTTP 400 results now reach `.subscribe()` |
| `app.routes.ts`, `app.html` | Route and left bar icon |
