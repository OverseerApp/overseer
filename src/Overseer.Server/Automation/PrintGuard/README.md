# PrintGuard Failure Detection

## Overview

This directory contains the PrintGuard failure detection implementation for Overseer, which provides real-time 3D print failure detection using machine learning. The implementation uses a computer vision model to analyze camera frames and detect print failures such as spaghetti prints, layer shifts, and other common 3D printing problems.

## Architecture

The failure detection system consists of several key components:

- **PrintGuardModel**: Manages the ONNX Runtime inference session and loads the pre-trained neural network model from HuggingFace
- **PrintGuardFailureDetectionAnalyzer**: Implements the failure detection logic using prototypical networks (few-shot learning approach)
- **PrintGuardCameraStreamer**: Handles camera feed capture and frame preprocessing
- **PrintGuardPrototypes**: Loads and manages the embedding prototypes for classification

## How It Works

The system uses a **prototypical network** approach for few-shot classification:

1. **Frame Capture**: Camera frames are captured and preprocessed to 224x224 RGB images
2. **Feature Extraction**: The pre-trained ShuffleNet feature extractor (running via ONNX Runtime) converts each frame into a high-dimensional embedding vector
3. **Classification**: The embedding is compared against prototype vectors (representing "success", "spaghetti", "layer shift", etc.) using Euclidean distance
4. **Temporal Filtering**: Results are aggregated over a sliding window (default: 20 frames) with a threshold of 70% to reduce false positives
5. **Failure Detection**: If the failure rate exceeds the threshold, a failure is reported with the most common failure type

## Attribution

This implementation is **based on and inspired by** the work of **Oliver Bravery**:

### Research Foundation

- **Research Paper**: "Few-shot Classification for Real-time Additive Manufacturing Fault Detection on Edge Devices" by Oliver Bravery
- **Model Training Repository**: [Edge-FDM-Fault-Detection](https://github.com/oliverbravery/Edge-FDM-Fault-Detection)
  - Contains the training code, feature extractor fine-tuning, and prototypical network training
  - Includes the technical dissertation (dissertation.pdf) with full research details
  - Licensed under GPL-2.0

### Original Implementation

- **PrintGuard Project**: [PrintGuard](https://github.com/oliverbravery/PrintGuard)
  - The original Python-based web application for 3D print monitoring and failure detection
  - Features a complete web interface, camera integration, and printer control
  - Available on PyPI: `pip install --pre printguard`
  - Licensed under GPL-2.0

### Key Differences

While this implementation is inspired by Oliver Bravery's PrintGuard, it has been adapted for integration into Overseer:

- **Language**: Ported from Python to C# for .NET integration
- **Framework**: Uses ONNX Runtime instead of PyTorch/ONNX Runtime Python bindings
- **Architecture**: Designed as a pluggable analyzer component within Overseer's monitoring system
- **Deployment**: Integrated into Overseer's existing multi-printer monitoring infrastructure

## Model Details

- **Feature Extractor**: ShuffleNet-based CNN fine-tuned on 3D printing datasets
- **Classification Method**: Prototypical Networks (few-shot learning)
- **Input Size**: 224x224 RGB images
- **Model Format**: ONNX
- **Model Source**: Downloaded automatically from HuggingFace (`oliverbravery/PrintGuard`)

## Usage

The PrintGuard analyzer is instantiated with a model and camera streamer, then called periodically to analyze frames:

```csharp
// Initialize
var model = new PrintGuardModel(httpClientFactory); // DI: singleton
var streamer = new PrintGuardCameraStreamer(); // DI: transient
var analyzer = new PrintGuardFailureDetectionAnalyzer(model, streamer); // DI: transient

// analyzer
analyzer.Start("http://camera-url/stream");

while (true)
{
  // Analyze frames
  var result = analyzer.Analyze();

  if (result.IsFailureDetected)
  {
      Console.WriteLine($"Failure detected: {result.FailureReason}");
      Console.WriteLine($"Confidence: {result.ConfidenceScore:P}");
      break;
  }
}

analyzer.Stop();
```

## Configuration

Key parameters (currently hardcoded in `PrintGuardFailureDetectionAnalyzer`):

- **Window Size**: 20 frames for temporal filtering
- **Failure Threshold**: 70% (0.7) of frames in window must indicate failure
- **Distance Normalization**: Divides by 10.0 for confidence score calculation

## Dependencies

- **Microsoft.ML.OnnxRuntime**: For running the ONNX model
- **Emgu.CV**: For camera capture and frame processing
- **SixLabors.ImageSharp**: For image preprocessing and transformations

## License

This implementation is part of Overseer. Please note that the original PrintGuard project and the Edge-FDM-Fault-Detection research repository are licensed under **GPL-2.0**, which may have implications for this derivative work.

## References

1. Bravery, O. (2024). _Few-shot Classification for Real-time Additive Manufacturing Fault Detection on Edge Devices_. [Dissertation PDF](https://github.com/oliverbravery/Edge-FDM-Fault-Detection/blob/main/dissertation.pdf)
2. PrintGuard GitHub Repository: https://github.com/oliverbravery/PrintGuard
3. Edge-FDM-Fault-Detection GitHub Repository: https://github.com/oliverbravery/Edge-FDM-Fault-Detection
4. PrintGuard on PyPI: https://pypi.org/project/printguard/

## Acknowledgments

Special thanks to **Oliver Bravery** for developing the PrintGuard system and publishing both the research and implementation as open source, making this adaptation possible.
