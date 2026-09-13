# Local TTS helper

This optional bridge runs Piper in a separate Python process. It sends one JSON
request over standard input and returns one JSON result over standard output, so
Godot and the domain model do not depend on Python or Piper.

Install a supported 64-bit Python with the Windows `py` launcher first. Run
`./setup.ps1 -Runtime Cuda` for an NVIDIA/RTX installation or
`./setup.ps1 -Runtime Cpu` for the CPU runtime. Package installation and voice
downloads are always explicit user actions. No model is downloaded by the app.

After setup, explicitly download a Piper voice into `tools/tts/models/` using the
command printed by the setup script, or configure a profile with an existing
`.onnx` path. The adjacent `.onnx.json` configuration file is required.

Generated WAV files are stored alongside the saved game project under
`audio/generated/`; JSON stores only that project-relative reference. CUDA is
preferred, and the bridge retries on CPU when CUDA is unavailable or fails.
