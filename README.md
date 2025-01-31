# Chaos Generator for NDI

**Tractus.Ndi.Chaos** is a console application that generates **NDI** video frames for testing and demonstration purposes. It supports customizable dimensions, frame rates, clock jitter, and various timecode modes.

The purpose is to create a "perfect" NDI source, and allow users to gradually mess with the source in order to introduce issues in applications that consume NDI.

In other words, it answers the question, "what happens when this application ingests an NDI source that misbehaves?"

---

## Usage

```
Tractus.Ndi.Chaos.exe [key=value] [key=value] ...
```

You can also type `Tractus.Ndi.Chaos.exe help` or **no arguments** to see usage instructions.

### Command-Line Parameters

- **width=&lt;int&gt;**
  - Sets the output frame width in pixels.
  - *Default:* `1920`.
- **height=&lt;int&gt;**
  - Sets the output frame height in pixels.
  - *Default:* `1080`.
- **fps=&lt;int&gt;**
  - Sets the frames per second.
  - *Default:* `30`.
- **clock=override**
  - Overrides the default NDI clock behavior.  
  - By default, the app relies on NDI’s internal clock timing.  
  - If you pass `clock=override`, **jitter** settings will take effect, and manual timing is enforced in the loop.
- **jlo=&lt;int&gt;**
  - Clock jitter lower bound (in milliseconds).
  - *Default:* `0`.
- **jhi=&lt;int&gt;**
  - Clock jitter upper bound (in milliseconds).
  - *Default:* `jlo` value if not provided.
- **timecode=&lt;mode&gt;**
  - Sets the timecode mode. Accepted values:
    - `invalid` &rarr; Always sets timecode to `1`.
    - `framecounter` &rarr; The timecode increments per frame.
    - `systemclock` &rarr; Uses `DateTime.Now.Ticks`.
    - `random` &rarr; Pseudorandom numbers.
    - *default* = `synthesize` (uses `NDIlib.send_timecode_synthesize`).
- **name=&lt;source&gt;**
  - Sets the NDI source name.
  - *Default:* `"Chaos"`.

### Example

```
Tractus.Ndi.Chaos.exe width=1280 height=720 fps=25 jlo=10 jhi=50 clock=override timecode=random name="ChaosTest"
```

This command:
- Generates 1280×720 output at 25 FPS,
- Has frame jitter from 10 to 50 ms due to `clock=override`,
- Uses a random timecode,
- Names the NDI source `"ChaosTest"`.

---

## Interactive Commands (Once Running)

When the application is running, you’ll see a prompt:  
```
Command >
```
Use the commands below to interact with the running sender:

- **q**  
  Quit the application gracefully.
- **s**  
  Requests an immediate stall (the application pauses sending for a random ms between 1 and 125).
- **s &lt;time&gt;**  
  Request a stall for exactly the specified number of milliseconds (`<time>`).
- **j &lt;low&gt; &lt;high&gt;**  
  Sets new jitter bounds if the clock is in override mode.
  - e.g., `j 20 50` → Jitter is now between 20 ms and 50 ms.
- **t &lt;type&gt;**  
  Change the timecode mode on-the-fly. Values:
  - **c** → System Clock (`DateTime.Now.Ticks`)
  - **s** → Synthesize (NDI Timecode)
  - **i** → Invalid (uses `1`)
  - **o** → Frame Counter
  - **r** → Random
- **?**  
  Displays the help text for interactive commands.

---

### Author

**Tractus.Ndi.Chaos** — *by Tractus Events*

NDI® is a registered trademark of Vizrt NDI AB.