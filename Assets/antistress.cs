using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Antistress;
using KModkit;
using UnityEngine;
using rnd = UnityEngine.Random;

public class antistress : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] mainButtons;
    public KMSelectable controlButton;
    public KMSelectable colorButton;
    public GameObject[] minigames;
    public Color[] buttonColors;

    public KMSelectable[] squishButtons;
    public Color[] squishButtonColors;
    public KMSelectable bigSwitch;
    public KMSelectable[] dials;
    public KMSelectable[] sticks;
    public Color[] stickStartingColors;
    public KMSelectable balloon;
    public KMSelectable[] pixels;
    private Renderer[] pixelRenders;
    public Renderer paintingLed;
    public KMSelectable[] paintingColorButtons;
    public Color[] paintingColors;

    public TextMesh readingTextMesh;
    public MeshRenderer readingTextRenderer;

    private int startingColor;
    private int solution;
    private int currentColor;
    private bool inGame;
    private static readonly string[] labels1 = new string[] { "Solve the module", "Switch & buttons", "Turnable dials", "Sorting colors" };
    private static readonly string[] labels2 = new string[] { "Balloon", "Pixel painting", "Library", "Under construction!" };

    private bool switchUp;
    private bool switchAnimating;
    private int[] dialPositions = new int[4];
    private static readonly int[] dialBounds = new int[] { 4, 8, 16, 32 };
    private int[] stickOrder = new int[6];
    private int? selectedStick = null;
    private bool stickAnimating;
    private Color selectedColor;
    private bool balloonHeld;
    private int balloonStage;
    private bool dragging;
    private Color currentPaintingColor;

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable button in mainButtons)
            button.OnInteract += delegate () { PressButton(button); return false; };
        controlButton.OnInteract += delegate () { PressControlButton(); return false; };
        colorButton.OnInteract += delegate () { PressColorButton(); return false; };
        pixelRenders = pixels.Select(x => x.GetComponent<Renderer>()).ToArray();

        foreach (KMSelectable button in squishButtons)
            button.OnInteract += delegate () { PressSquishButton(button); return false; };
        bigSwitch.OnInteract += delegate () { FlipSwitch(); return false; };
        foreach (KMSelectable dial in dials)
            dial.OnInteract += delegate () { PressDial(dial); return false; };
        foreach (KMSelectable stick in sticks)
            stick.OnInteract += delegate () { PressStick(stick); return false; };
        balloon.OnInteract += delegate () { balloonHeld = true; return false; };
        balloon.OnInteractEnded += delegate () { balloonHeld = false; };
        foreach (KMSelectable pixel in pixels)
        {
            pixel.OnInteract += delegate () { PaintPixel(pixel); dragging = true; return false; };
            pixel.OnInteractEnded += delegate () { dragging = false; };
            pixel.OnHighlight += delegate () { if (dragging) { PaintPixel(pixel); } };
        }
        foreach (KMSelectable button in paintingColorButtons)
        {
            button.OnInteract += delegate () { PressPaintingButton(button); return false; };
            button.GetComponent<Renderer>().material.color = paintingColors[Array.IndexOf(paintingColorButtons, button)];
        }
    }

    private void Start()
    {
        foreach (GameObject game in minigames)
            game.SetActive(false);
        foreach (KMSelectable button in squishButtons)
            button.GetComponent<Renderer>().material.color = squishButtonColors[Array.IndexOf(squishButtons, button)];
        foreach (Renderer pixel in pixelRenders)
            pixel.material.color = Color.white;
        StartCoroutine(StartStickAnimations());
        ScrambleSticks();

        startingColor = rnd.Range(0, 7);
        currentColor = startingColor;
        var sn = bomb.GetSerialNumberNumbers().Last();
        var table = "4124679523154798359652356982106904836800516825801217379467320109434778";
        solution = int.Parse(table[sn * 7 + startingColor].ToString());
        Debug.LogFormat("[Antistress #{0}] Starting color index (in table): {1}", moduleId, startingColor + 1);
        Debug.LogFormat("[Antistress #{0}] Last digit of the serial number: {1}", moduleId, sn);
        Debug.LogFormat("[Antistress #{0}] Solution digit: {1}", moduleId, solution);
        UpdateColors();

        SetWordWrappedText(mobyDick.book[0]);
    }

    private void PressButton(KMSelectable button)
    {
        button.AddInteractionPunch(.25f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
        var label = button.GetComponentInChildren<TextMesh>().text;
        if (label == "Solve the module")
        {
            if (moduleSolved)
            {
                Debug.LogFormat("[Antistress #{0}] You already solved the module, silly!", moduleId);
                return;
            }
            var submitted = ((int)bomb.GetTime()) % 10;
            Debug.LogFormat("[Antistress #{0}] Attempted to solve the module on a {1}.", moduleId, submitted);
            if (submitted == solution)
            {
                module.HandlePass();
                moduleSolved = true;
                Debug.LogFormat("[Antistress #{0}] Module solved. Keep up the good work!", moduleId);
            }
            else
            {
                module.HandleStrike();
                Debug.LogFormat("[Antistress #{0}] Strike. Try again.", moduleId);
                currentColor = startingColor;
                UpdateColors();
            }
        }
        else if (label == "Under construction!")
            return;
        else
        {
            inGame = true;
            foreach (KMSelectable b in mainButtons)
                b.gameObject.SetActive(false);
            minigames[Array.IndexOf(labels1.Concat(labels2).ToArray(), label) - 1].SetActive(true);
            colorButton.gameObject.SetActive(false);
        }
    }

    private void PressControlButton()
    {
        controlButton.AddInteractionPunch(.25f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, controlButton.transform);
        if (inGame)
        {
            inGame = false;
            foreach (KMSelectable button in mainButtons)
                button.gameObject.SetActive(true);
            foreach (GameObject game in minigames)
                game.SetActive(false);
            colorButton.GetComponentInChildren<TextMesh>().text = "c";
            colorButton.gameObject.SetActive(true);
        }
        else
        {
            var page1 = mainButtons[0].GetComponentInChildren<TextMesh>().text == "Solve the module";
            for (int i = 0; i < 4; i++)
                mainButtons[i].GetComponentInChildren<TextMesh>().text = page1 ? labels2[i] : labels1[i];
        }
    }

    private void PressColorButton()
    {
        colorButton.AddInteractionPunch(.25f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, colorButton.transform);
        currentColor = (currentColor + 1) % 7;
        UpdateColors();
    }

    private void PressSquishButton(KMSelectable button)
    {
        button.AddInteractionPunch();
        audio.PlaySoundAtTransform("squish", button.transform);
    }

    private void FlipSwitch()
    {
        if (switchAnimating)
            return;
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, bigSwitch.transform);
        bigSwitch.AddInteractionPunch(.25f);
        switchUp = !switchUp;
        StartCoroutine(MoveSwitch());
    }

    private void PressDial(KMSelectable dial)
    {
        dial.AddInteractionPunch(.25f);
        audio.PlaySoundAtTransform("dial click", dial.transform);
        var ix = Array.IndexOf(dials, dial);
        dialPositions[ix] = (dialPositions[ix] + 1) % dialBounds[ix];
        var rotation = 360f / dialBounds[ix] * dialPositions[ix];
        dial.transform.localEulerAngles = new Vector3(0f, rotation, 0f);
    }

    private IEnumerator MoveSwitch()
    {
        var elapsed = 0f;
        var duration = .3f;
        var startAngle = switchUp ? 55f : -55f;
        var endAngle = startAngle * -1f;
        switchAnimating = true;
        while (elapsed < duration)
        {
            bigSwitch.transform.localEulerAngles = new Vector3(Easing.OutSine(elapsed, startAngle, endAngle, duration), 0f, 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        bigSwitch.transform.localEulerAngles = new Vector3(endAngle, 0f, 0f);
        switchAnimating = false;
    }

    private IEnumerator StartStickAnimations()
    {
        for (int i = 0; i < 6; i++)
        {
            StartCoroutine(AnimateStick(sticks[i].transform));
            yield return new WaitForSeconds(.2f);
        }
    }

    private void PressStick(KMSelectable stick)
    {
        if (stickAnimating)
            return;
        var ix = Array.IndexOf(sticks, stick);
        if (selectedStick == null)
        {
            selectedStick = ix;
            selectedColor = stick.GetComponent<Renderer>().material.color;
            stick.GetComponent<Renderer>().material.color = Color.white;
            audio.PlaySoundAtTransform("splat", stick.transform);
        }
        else
        {
            var a = stickOrder[(int)selectedStick];
            stickOrder[(int)selectedStick] = stickOrder[ix];
            stickOrder[ix] = a;
            sticks[(int)selectedStick].GetComponent<Renderer>().material.color = stick.GetComponent<Renderer>().material.color;
            stick.GetComponent<Renderer>().material.color = selectedColor;
            audio.PlaySoundAtTransform("splat", stick.transform);
            selectedStick = null;
            if (stickOrder.SequenceEqual(new int[] { 5, 4, 3, 2, 1, 0 }))
                StartCoroutine(ResetSticks());
        }
    }

    private IEnumerator ResetSticks()
    {
        stickAnimating = true;
        yield return new WaitForSeconds(.6f);
        for (int i = 0; i < 6; i++)
        {
            sticks[i].GetComponent<Renderer>().material.color = Color.black;
            audio.PlaySoundAtTransform("donk", transform);
            yield return new WaitForSeconds(.4f);
        }
        yield return new WaitForSeconds(.6f);
        stickAnimating = false;
        audio.PlaySoundAtTransform("thud", transform);
        ScrambleSticks();
    }

    private void ScrambleSticks()
    {
        stickOrder = Enumerable.Range(0, 6).ToList().Shuffle().ToArray();
        var colors = new Color[6];
        float H, S, V;
        Color.RGBToHSV(stickStartingColors.PickRandom(), out H, out S, out V);
        for (int i = 0; i < 6; i++)
            colors[i] = Color.HSVToRGB(H, S, V - i * .15f);
        for (int i = 0; i < 6; i++)
            sticks[i].GetComponent<Renderer>().material.color = colors[stickOrder[i]];
    }

    private IEnumerator AnimateStick(Transform stick)
    {
        var x = stick.localPosition.x;
        var y = stick.localPosition.y;
        var elapsed = 0f;
        var duration = 3.5f;
        while (true)
        {
            elapsed = 0f;
            while (elapsed < duration)
            {
                stick.localPosition = new Vector3(x, y, Easing.InOutSine(elapsed, -.0279f, -.0102f, duration));
                yield return null;
                elapsed += Time.deltaTime;
                stick.localPosition = new Vector3(x, y, -.0102f);
            }
            elapsed = 0f;
            while (elapsed < duration)
            {
                stick.localPosition = new Vector3(x, y, Easing.InOutSine(elapsed, -.0102f, -.0279f, duration));
                yield return null;
                elapsed += Time.deltaTime;
                stick.localPosition = new Vector3(x, y, -.0279f);
            }
        }
    }

    private void PaintPixel(KMSelectable pixel)
    {
        pixelRenders[Array.IndexOf(pixels, pixel)].material.color = currentPaintingColor;
    }

    private void PressPaintingButton(KMSelectable button)
    {
        button.AddInteractionPunch(.25f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
        var ix = Array.IndexOf(paintingColorButtons, button);
        currentPaintingColor = paintingColors[ix];
        paintingLed.material.color = paintingColors[ix];
    }

    private void SetWordWrappedText(string text)
    {
        var acceptableWidth = 80d;
        var desiredHeight = 150d;

        var low = 1;
        var high = 1024;
        var wrappeds = new Dictionary<int, string>();

        var prevPosition = readingTextMesh.transform.localPosition;
        var prevRotation = readingTextMesh.transform.localRotation;
        var prevScale = readingTextMesh.transform.localScale;
        var parent = readingTextMesh.transform.parent;

        readingTextMesh.transform.SetParent(null, false);
        readingTextMesh.transform.localPosition = new Vector3(0, 0, 0);
        readingTextMesh.transform.localRotation = Quaternion.Euler(90, 0, 0);
        readingTextMesh.transform.localScale = new Vector3(1, 1, 1);

        while (high - low > 1)
        {
            var mid = (low + high) / 2;
            readingTextMesh.fontSize = mid;
            readingTextMesh.text = "\u00a0";
            var widthOfASpace = readingTextRenderer.bounds.size.x;

            var wrappedSB = new StringBuilder();
            var first = true;
            foreach (var line in Ut.WordWrap(
                text,
                line => acceptableWidth,
                widthOfASpace,
                str =>
                {
                    readingTextMesh.text = str;
                    return readingTextRenderer.bounds.size.x;
                },
                allowBreakingWordsApart: false
            ))
            {
                if (line == null)
                {
                    // There was a word that was too long to fit into a line.
                    high = mid;
                    wrappedSB = null;
                    break;
                }
                if (!first)
                    wrappedSB.Append('\n');
                first = false;
                wrappedSB.Append(line);
            }

            if (wrappedSB != null)
            {
                var wrapped = wrappedSB.ToString();
                wrappeds[mid] = wrapped;
                readingTextMesh.text = wrapped;
                if (readingTextRenderer.bounds.size.z > desiredHeight)
                    high = mid;
                else
                    low = mid;
            }
        }
        readingTextMesh.fontSize = low;
        readingTextMesh.text = wrappeds[low];
        readingTextMesh.transform.SetParent(parent, false);
        readingTextMesh.transform.localPosition = prevPosition;
        readingTextMesh.transform.localRotation = prevRotation;
        readingTextMesh.transform.localScale = prevScale;
    }

    private void UpdateColors()
    {
        foreach (Renderer button in mainButtons.Select(x => x.GetComponent<Renderer>()))
            button.material.color = buttonColors[currentColor];
    }

    private void Update()
    {
        if (balloonHeld && balloonStage < 60)
            balloonStage++;
        if (!balloonHeld && balloonStage > 0)
            balloonStage--;
        var balloonScale = Mathf.Lerp(.02f, .1f, balloonStage / 60f);
        balloon.transform.localScale = new Vector3(balloonScale, 0.0006350432f, balloonScale);
    }
}
