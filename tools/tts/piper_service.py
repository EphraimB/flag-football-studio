"""One-request JSON/stdin bridge for local Piper speech generation.

This helper never downloads models. A caller must provide an existing .onnx model
and adjacent .onnx.json config. It prefers CUDA when requested and can explicitly
fall back to CPU.
"""

from __future__ import annotations

import json
import math
import sys
import wave
from pathlib import Path


def _device_available(name: str) -> bool:
    import onnxruntime

    return name in onnxruntime.get_available_providers()


def _load_voice(model_path: str, prefer_cuda: bool, allow_cpu_fallback: bool):
    from piper import PiperVoice

    if prefer_cuda and _device_available("CUDAExecutionProvider"):
        try:
            return PiperVoice.load(model_path, use_cuda=True, include_alignments=True), "cuda"
        except Exception:
            if not allow_cpu_fallback:
                raise
    if prefer_cuda and not allow_cpu_fallback:
        raise RuntimeError("CUDAExecutionProvider is unavailable and CPU fallback is disabled.")
    return PiperVoice.load(model_path, use_cuda=False, include_alignments=True), "cpu"


def _generate(request: dict) -> dict:
    from piper.config import SynthesisConfig

    model = Path(request["model_path"])
    config = Path(str(model) + ".json")
    if not model.is_file() or not config.is_file():
        raise FileNotFoundError("Piper requires an existing .onnx model and adjacent .onnx.json config.")
    output = Path(request["output_wave_path"])
    output.parent.mkdir(parents=True, exist_ok=True)
    if output.exists():
        raise FileExistsError("Refusing to overwrite an existing generated audio file.")

    voice, device = _load_voice(
        str(model), bool(request.get("prefer_cuda", True)), bool(request.get("allow_cpu_fallback", True))
    )
    speaker = request.get("speaker_id")
    synthesis = SynthesisConfig(
        speaker_id=int(speaker) if speaker not in (None, "") else None,
        length_scale=1.0 / max(0.5, min(2.0, float(request.get("speaking_rate", 1.0)))),
        volume=max(0.0, min(1.0, float(request.get("volume", 1.0)))),
    )
    with wave.open(str(output), "wb") as wav_file:
        alignments = voice.synthesize_wav(
            request["text"], wav_file, syn_config=synthesis, include_alignments=True
        )

    phonemes = []
    cursor_samples = 0
    sample_rate = int(voice.config.sample_rate)
    for alignment in alignments or []:
        sample_count = int(getattr(alignment, "num_samples", 0))
        phonemes.append(
            {
                "phoneme": str(getattr(alignment, "phoneme", "")),
                "start_time": cursor_samples / sample_rate,
                "end_time": (cursor_samples + sample_count) / sample_rate,
            }
        )
        cursor_samples += sample_count
    with wave.open(str(output), "rb") as wav_file:
        duration = wav_file.getnframes() / float(wav_file.getframerate())
        sample_rate = wav_file.getframerate()
    return {
        "success": True,
        "output_wave_path": str(output),
        "duration": duration,
        "sample_rate": sample_rate,
        "device": device,
        "phonemes": phonemes,
        "error": None,
    }


def main() -> int:
    try:
        request = json.loads(sys.stdin.readline())
        if request.get("command") != "generate":
            raise ValueError("Unsupported command.")
        print(json.dumps(_generate(request)), flush=True)
        return 0
    except Exception as error:
        print(json.dumps({"success": False, "error": str(error), "phonemes": []}), flush=True)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
