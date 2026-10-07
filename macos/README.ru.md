# SuperDictate для Mac

[English](README.md) · **Русский**

Диктовка в любом приложении на Mac. Нажмите **правый ⌘**, говорите, нажмите его
ещё раз — и слова напечатаются там, где стоит курсор. Речь распознаётся на
самом Mac, нейросетевым ядром Apple: голос никуда не отправляется.

- **Установка и использование:** [инструкция для Mac](../docs/mac-guide.ru.md),
  шаг за шагом — от загрузки до первой диктовки.
- **Скачать:** `SuperDictate-macOS-<версия>.zip` со страницы
  [последнего выпуска](https://github.com/ZazaKin/SuperDictate-windows/releases/latest).
  Нужен Mac на Apple Silicon (M1 или новее) и macOS 14 или новее.

В этой папке — исходный код приложения: пакет Swift на SwiftUI и AppKit и
распознавание речи NVIDIA Parakeet TDT v3 через
[FluidAudio](https://github.com/FluidInference/FluidAudio).

```bash
swift test --package-path macos              # тесты основной логики
bash macos/scripts/build-app.sh              # macos/dist/SuperDictate.app и zip
bash macos/scripts/install-local.sh          # заменить /Applications/SuperDictate.app
```

Устройство кода, сборка на GitHub со снимками экрана и то, что общее у версий
для Mac и Windows, — в [docs/development.ru.md](../docs/development.ru.md).

## Благодарности

Распознавание речи: [NVIDIA Parakeet TDT 0.6B v3](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3)
(CC BY 4.0), переведена в Core ML командой FluidInference и работает через
[FluidAudio](https://github.com/FluidInference/FluidAudio) (Apache 2.0).
Основано на [Parakey](https://github.com/rcourtman/parakey) Ричарда Кортмана (MIT).
