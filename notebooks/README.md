# Notebooks

This folder contains interactive notebooks for the workshop.

Quick start:
- Run the VS Code task: "Setup: Jupyter + .NET Interactive" (installs Jupyter in a local venv and registers the .NET kernels).
- Then run the task: "Open: Jupyter Lab".
- Open `chroma_csharp.ipynb` and choose the kernel: `.NET (C#)`.

Manual commands (PowerShell):

1) Setup
```
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/setup-jupyter.ps1
```

2) Launch Jupyter Lab rooted in this notebooks folder
```
.\.venv\Scripts\jupyter-lab notebooks
```
