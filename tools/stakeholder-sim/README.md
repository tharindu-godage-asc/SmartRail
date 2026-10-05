# Stakeholder simulation

## Setup

From the repository root, create and activate the virtual environment:

```powershell
python -m venv tools/stakeholder-sim/.venv
.\tools\stakeholder-sim\.venv\Scripts\Activate.ps1
python -m pip install -r tools/stakeholder-sim/requirements.txt
```

Create `tools/stakeholder-sim/.env` from `.env.example` and set `GOOGLE_API_KEY`
to an API key created in [Google AI Studio](https://aistudio.google.com/app/apikey).
The `.env` file is ignored by Git.

The selected model is `gemini-2.5-flash`, listed as Gemini 2.5 Flash on the
[current Gemini API models page](https://ai.google.dev/gemini-api/docs/models).
Check the models page before upgrading or changing this identifier.
