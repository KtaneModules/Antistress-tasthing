using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    public TextMesh colorblindText;

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
    public KMSelectable clearButton;
    public KMSelectable[] paintingColorButtons;
    public Color[] paintingColors;
    public TextMesh readingTextMesh;
    public MeshRenderer readingTextRenderer;
    public Renderer readingScreen;
    public Texture[] bookCovers;
    public Color pageColor;
    public KMSelectable bookButton;
    public KMSelectable[] bookCycleButtons;

    private int startingColor;
    private int solution;
    private int currentColor;
    private bool inGame;
    private static readonly string[] labels1 = new[] { "Solve the module", "Switch & buttons", "Turnable dials", "Sorting colors" };
    private static readonly string[] labels2 = new[] { "Balloon", "Pixel painting", "Library", "Under construction!" };
    private static readonly string[] colorNames = new[] { "green", "dark blue", "pink", "orange", "yellow", "magenta", "light blue" };

    private bool switchUp = true;
    private int[] dialPositions = new int[4];
    private static readonly int[] dialBounds = new[] { 4, 8, 16, 32 };
    private int[] stickOrder = new int[6];
    private int? selectedStick = null;
    private bool stickAnimating;
    private Color selectedColor;
    private bool balloonHeld;
    private int balloonStage;
    private bool dragging;
    private Color currentPaintingColor;
    private int selectedBook;
    private int pageNumber;
    private bool bookSelected;
    private string[] currentBook = null;

    private Coroutine switchMovement;
    private Coroutine[] dialMovements = new Coroutine[4];
    private int storedTime;
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;
    private bool colorblindMode;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable button in mainButtons)
            button.OnInteract += delegate () { PressButton(button); return false; };
        controlButton.OnInteract += delegate () { PressControlButton(); return false; };
        colorButton.OnInteract += delegate () { storedTime = (int)bomb.GetTime(); return false; };
        colorButton.OnInteractEnded += delegate () { ReleaseColorButton(); };
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
        clearButton.OnInteract += delegate () { PressClearButton(); return false; };
        foreach (KMSelectable button in paintingColorButtons)
        {
            button.OnInteract += delegate () { PressPaintingButton(button); return false; };
            button.GetComponent<Renderer>().material.color = paintingColors[Array.IndexOf(paintingColorButtons, button)];
        }
        bookButton.OnInteract += delegate () { PressBookButton(); return false; };
        foreach (KMSelectable button in bookCycleButtons)
            button.OnInteract += delegate () { PressBookButton(button); return false; };
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
        readingTextMesh.text = "";
        readingScreen.material.color = Color.white;
        readingScreen.material.mainTexture = bookCovers[0];
        colorblindMode = GetComponent<KMColorblindMode>().ColorblindModeActive;
        SetColorblindModeText(colorblindMode);

        startingColor = rnd.Range(0, 7);
        currentColor = startingColor;
        var sn = bomb.GetSerialNumberNumbers().Last();
        var table = "4124679523154798359652356982106904836800516825801217379467320109434778";
        solution = int.Parse(table[sn * 7 + startingColor].ToString());
        Debug.LogFormat("[Antistress #{0}] Starting color index (in table): {1}", moduleId, startingColor + 1);
        Debug.LogFormat("[Antistress #{0}] Last digit of the serial number: {1}", moduleId, sn);
        Debug.LogFormat("[Antistress #{0}] Solution digit: {1}", moduleId, solution);
        UpdateColors();
        _tpCode = GetModuleCode();
    }

    private void SetColorblindModeText(bool mode)
    {
        colorblindText.gameObject.SetActive(mode);
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
            colorblindText.gameObject.SetActive(false);
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
            SetColorblindModeText(colorblindMode);
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

    private void ReleaseColorButton()
    {
        colorButton.AddInteractionPunch(.25f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, colorButton.transform);
        if (storedTime == (int)bomb.GetTime())
            ToggleColors();
        else
        {
            currentColor = startingColor;
            UpdateColors();
        }
    }

    private void ToggleColors()
    {
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
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, bigSwitch.transform);
        bigSwitch.AddInteractionPunch(.25f);
        switchUp = !switchUp;
        if (switchMovement != null)
        {
            StopCoroutine(switchMovement);
            switchMovement = null;
        }
        switchMovement = StartCoroutine(MoveSwitch());
    }

    private void PressDial(KMSelectable dial)
    {
        dial.AddInteractionPunch(.25f);
        audio.PlaySoundAtTransform("dial click", dial.transform);
        var ix = Array.IndexOf(dials, dial);
        dialPositions[ix] = (dialPositions[ix] + 1) % dialBounds[ix];
        if (dialMovements[ix] != null)
        {
            StopCoroutine(dialMovements[ix]);
            dialMovements[ix] = null;
        }
        var start = dial.transform.localRotation;
        var end = Quaternion.Euler(new Vector3(0f, 360f / dialBounds[ix] * dialPositions[ix], 0f));
        dialMovements[ix] = StartCoroutine(MoveDial(dialBounds[ix], start, end, dial.transform));
    }

    private IEnumerator MoveDial(int bound, Quaternion start, Quaternion end, Transform dial)
    {
        var elapsed = 0f;
        var duration = 1f / bound;
        while (elapsed < duration)
        {
            dial.localRotation = Quaternion.Slerp(start, end, elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        dial.localRotation = end;
    }

    private IEnumerator MoveSwitch()
    {
        var elapsed = 0f;
        var duration = .25f;
        var startAngle = bigSwitch.transform.localEulerAngles.x;
        var endAngle = switchUp ? 55f : -55f;
        while (elapsed < duration)
        {
            //  bigSwitch.transform.localEulerAngles = new Vector3(Easing.OutSine(elapsed, startAngle, endAngle, duration), 0f, 0f);
            bigSwitch.transform.localRotation = Quaternion.Slerp(Quaternion.Euler(startAngle, 0f, 0f), Quaternion.Euler(endAngle, 0f, 0f), elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        bigSwitch.transform.localEulerAngles = new Vector3(endAngle, 0f, 0f);
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
            if (stickOrder.SequenceEqual(new[] { 5, 4, 3, 2, 1, 0 }) || stickOrder.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }))
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
        while (stickOrder.SequenceEqual(new[] { 5, 4, 3, 2, 1, 0 }) || stickOrder.SequenceEqual(new[] { 0, 1, 2, 3, 4, 5 }))
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

    private void PressClearButton()
    {
        clearButton.AddInteractionPunch(.25f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, clearButton.transform);
        var prevColor = currentPaintingColor;
        currentPaintingColor = paintingColors[15];
        foreach (KMSelectable pixel in pixels)
            PaintPixel(pixel);
        currentPaintingColor = prevColor;
    }

    private void PressBookButton()
    {
        bookButton.AddInteractionPunch(.25f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, bookButton.transform);
        if (!bookSelected)
        {
            bookButton.GetComponentInChildren<TextMesh>().text = "Exit";
            bookSelected = true;
            switch (selectedBook)
            {
                case 0:
                    currentBook = nineteenEightyFour.book.ToArray();
                    break;
                case 1:
                    currentBook = christmasCarol.book.ToArray();
                    break;
                case 2:
                    currentBook = callOfCthulhu.book.ToArray();
                    break;
                case 3:
                    currentBook = diaryOfAWimpyKid.book.ToArray();
                    break;
                case 4:
                    currentBook = fahrenheit451.book.ToArray();
                    break;
                case 5:
                    currentBook = mobyDick.book.ToArray();
                    break;
                case 6:
                    currentBook = bells.book.ToArray();
                    break;
                case 7:
                    currentBook = caskOfAmontillado.book.ToArray();
                    break;
                case 8:
                    currentBook = onesWhoWalkAwayFromOmelas.book.ToArray();
                    break;
                case 9:
                    currentBook = raven.book.ToArray();
                    break;
            }
            readingScreen.material.color = pageColor;
            readingScreen.material.mainTexture = null;
            SetWordWrappedText(currentBook[0]);
        }
        else
        {
            bookButton.GetComponentInChildren<TextMesh>().text = "Select";
            bookSelected = false;
            pageNumber = 0;
            currentBook = null;
            readingScreen.material.color = Color.white;
            readingScreen.material.mainTexture = bookCovers[selectedBook];
            readingTextMesh.text = "";
        }
    }

    private void PressBookButton(KMSelectable button)
    {
        button.AddInteractionPunch(.25f);
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
        var ix = Array.IndexOf(bookCycleButtons, button);
        var offsets = new[] { 1, -1 };
        if (!bookSelected)
        {
            selectedBook = (selectedBook + 10 + offsets[ix]) % 10;
            readingScreen.material.mainTexture = bookCovers[selectedBook];
        }
        else
        {
            if (ix == 0)
            {
                if (pageNumber == currentBook.Length - 1)
                    return;
                pageNumber++;
            }
            else
            {
                if (pageNumber == 0)
                    return;
                pageNumber--;
            }
            SetWordWrappedText(currentBook[pageNumber]);
        }
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
        colorblindText.text = colorNames[currentColor];
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

    // Twitch Plays, written by Quinn Wuest
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} cycle <#> [Press the C button # times.] | !{0} submit <#> [Press \"Solve the module\" when the last digit of the timer is #. | !{0} cycle reset [Resets the color to the initial color.]\n!{0} games [List all the different games to play] | !{0} game help [Give the help message for the current minigame.] !{0} return [Return to the main screen";
#pragma warning restore 414

    private string _tpCode;

    private int GetCurrentGame()
    {
        int currentGame = -1;
        for (int i = 0; i < minigames.Length; i++)
            if (minigames[i].activeInHierarchy)
                currentGame = i;
        return currentGame;
    }

    private IEnumerator ProcessTwitchCommand(string command)
    {
        // Initial declaration
        Match m;
        command = command.Trim().ToLowerInvariant();

        // Colorblind
        m = Regex.Match(command, @"^\s*(colou?rblind|cb)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            colorblindMode = !colorblindMode;
            if (!inGame)
                SetColorblindModeText(colorblindMode);
            yield break;
        }

        // Submit an answer
        m = Regex.Match(command, @"^\s*(?:submit\s+)(?<d>\d)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            if (inGame)
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text != "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            int num = int.Parse(m.Groups["d"].Value);
            while ((int)bomb.GetTime() % 10 != num)
                yield return null;
            mainButtons[0].OnInteract();
            yield break;
        }

        // Cycle colors x times
        m = Regex.Match(command, @"^\s*(?:cycle\s+)(?<d>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            int num;
            if (!int.TryParse(m.Groups["d"].Value, out num) || num < 0)
                yield break;
            yield return null;
            num = num % 7 == 0 ? 7 : (num % 7);
            if (inGame)
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text != "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            for (int i = 0; i < num; i++)
            {
                colorButton.OnInteract();
                yield return null;
                colorButton.OnInteractEnded();
                yield return new WaitForSeconds(0.75f);
                yield return "trycancel";
            }
            yield break;
        }

        // Cycle reset
        m = Regex.Match(command, @"^\s*cycle\s+reset\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            if (inGame)
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text != "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            colorButton.OnInteract();
            yield return new WaitUntil(() => (int)bomb.GetTime() != storedTime);
            colorButton.OnInteractEnded();
        }

        // Return
        m = Regex.Match(command, @"^\s*return\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            if (inGame)
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text != "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }

        // Game help
        int currentGame = GetCurrentGame();
        m = Regex.Match(command, @"^\s*game\s+help\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame == -1)
            {
                yield return "sendtochaterror You are not in a current game!";
                yield break;
            }
            yield return null;
            if (currentGame == 0) // Switch and Buttons
            {
                yield return "sendtochat Antistress: !" + _tpCode + " press red/yellow/green/blue [Press the button of this color.] | !" + _tpCode + " flip switch [Flip the switch.]";
                yield break;
            }
            if (currentGame == 1) // Dials
            {
                yield return "sendtochat Antistress: !" + _tpCode + " turn top/left/right/down 4 [Turn the dial this many times.]";
                yield break;
            }
            if (currentGame == 2) // Sorting colors
            {
                yield return "sendtochat Antistress: !" + _tpCode + " swap 1 3 [Swap keys 1 and 3.] | Commands can be chained using commas.";
                yield break;
            }
            if (currentGame == 3) // Balloon
            {
                yield return "sendtochat Antistress: !" + _tpCode + " inflate for 1.3 [Inflate the balloon for 1.3 seconds.] | The balloon must be inflated for more than 0 seconds and less than 5 seconds.";
                yield break;
            }
            if (currentGame == 4) // Pixel painting
            {
                yield return "sendtochat Antistress: !" + _tpCode + " pick color b4 [Pick the color at position B4.] | !" + _tpCode + " paint a5 dddrr [Paint at coordinate A5, then draw Down, Down, Down, Right, Right.] | !" + _tpCode + " clear [Clear the painting.]";
                yield break;
            }
            if (currentGame == 5) // Library
            {
                yield return "sendtochat Antistress: !" + _tpCode + " prev/next [Go to the previous/next book.] | !" + _tpCode + " select [Select the current book.] | !" + _tpCode + " select The Cask of Amontillado [Select the book with that title.] | Book titles are: 1984, A Christmas Carol, Call of Cthulhu, Diary of a Wimpy Kid, Fahrenheit 451, Moby Dick, The Bells, The Cask of Amontillado, The Ones who Walk Away from Omelas, The Raven";
                yield break;
            }
        }
        m = Regex.Match(command, @"^\s*games\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            yield return "sendtochat Antistress: List of games: \"Switch and buttons\", \"Turnable dials\", \"Sorting colors\", \"Balloon\", \"Pixel painting\", \"Library\"";
            yield break;
        }
        // Switch and buttons
        m = Regex.Match(command, @"^\s*switch\s+and\s+buttons\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            if (inGame)
            {
                if (currentGame == 0)
                    yield break;
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text != "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            mainButtons[1].OnInteract();
        }
        m = Regex.Match(command, @"^\s*press\s+(?<color>red|blue|green|yellow)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame != 0)
            {
                yield return "sendtochaterror You are not in the \"Switch & buttons\" minigame!";
                yield break;
            }
            yield return null;
            var color = m.Groups["color"].Value;
            var colors = new string[] { "red", "blue", "green", "yellow" };
            int ix = Array.IndexOf(colors, color);
            squishButtons[ix].OnInteract();
            yield break;
        }
        m = Regex.Match(command, @"^\s*flip\s+switch\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame != 0)
            {
                yield return "sendtochaterror You are not in the \"Switch & buttons\" minigame!";
                yield break;
            }
            yield return null;
            bigSwitch.OnInteract();
            yield break;
        }

        // Turnable dials
        m = Regex.Match(command, @"^\s*turnable\s+dials\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            if (inGame)
            {
                if (currentGame == 1)
                    yield break;
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text != "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            mainButtons[2].OnInteract();
        }
        m = Regex.Match(command, @"^\s*turn\s+(?<dial>right|top|bottom|left)\s+(?<d>\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame != 1)
            {
                yield return "sendtochaterror You are not in the \"Turnable dials\" minigame!";
                yield break;
            }
            int num;
            if (!int.TryParse(m.Groups["d"].Value, out num) || num < 0)
                yield break;
            yield return null;
            var dir = m.Groups["dial"].Value;
            var dirs = new string[] { "right", "top", "bottom", "left" };
            int ix = Array.IndexOf(dirs, dir);
            for (int i = 0; i < num % dialBounds[ix]; i++)
            {
                dials[ix].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }

        // Sorting colors
        m = Regex.Match(command, @"^\s*sorting\s+colors\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            if (inGame)
            {
                if (currentGame == 2)
                    yield break;
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text != "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            mainButtons[3].OnInteract();
        }
        var colorSwapParameters = command.Split(new[] { ',', ';' });
        for (int i = 0; i < colorSwapParameters.Length; i++)
        {
            m = Regex.Match(colorSwapParameters[i], @"^\s*swap\s+(?<d1>\d)\s+(?<d2>\d)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (m.Success)
            {
                if (currentGame != 2)
                {
                    yield return "sendtochaterror You are not in the \"Sorting colors\" minigame!";
                    yield break;
                }
                int num1 = int.Parse(m.Groups["d1"].Value);
                int num2 = int.Parse(m.Groups["d2"].Value);
                if (num1 < 1 || num2 < 1 || num1 > 6 || num2 > 6 || num1 == num2)
                {
                    yield return "sendtochaterror Invalid swap command: " + colorSwapParameters[i];
                    yield break;
                }
                yield return null;
                sticks[num1 - 1].OnInteract();
                yield return new WaitForSeconds(0.1f);
                sticks[num2 - 1].OnInteract();
                yield return new WaitForSeconds(0.4f);
                if (stickAnimating)
                    yield break;
            }
        }

        // Balloon
        m = Regex.Match(command, @"^\s*balloon\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            if (inGame)
            {
                if (currentGame == 3)
                    yield break;
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text == "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            mainButtons[0].OnInteract();
        }
        var balloonParameters = command.Split(' ');
        if (balloonParameters.Length == 3 && balloonParameters[0] == "inflate" && balloonParameters[1] == "for")
        {
            float num;
            if (!float.TryParse(balloonParameters[2], out num) || num <= 0 || num > 5)
                yield break;
            if (currentGame != 3)
            {
                yield return "sendtochaterror You are not in the \"Balloon\" minigame!";
                yield break;
            }
            yield return null;
            balloon.OnInteract();
            yield return new WaitForSeconds(num);
            balloon.OnInteractEnded();
        }

        // Pixel painting
        m = Regex.Match(command, @"^\s*pixel\s+painting\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            if (inGame)
            {
                if (currentGame == 4)
                    yield break;
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text == "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            mainButtons[1].OnInteract();
        }
        m = Regex.Match(command, @"^\s*pick\s+color\s+(?<col>[ABC])(?<row>[123456])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame != 4)
            {
                yield return "sendtochaterror You are not in the \"Pixel painting\" minigame!";
                yield break;
            }
            int col = "abc".IndexOf(m.Groups["col"].Value);
            int row = "123456".IndexOf(m.Groups["row"].Value);
            if (col == -1 || row == -1)
                yield break;
            yield return null;
            paintingColorButtons[row * 3 + col].OnInteract();
            yield break;
        }
        m = Regex.Match(command, @"^\s*paint\s+(?<col>[abcdefghijklmnopqrst])(?<row>\d+)(?<dirs>\s+[urdl ]+)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame != 4)
            {
                yield return "sendtochaterror You are not in the \"Pixel painting\" minigame!";
                yield break;
            }
            int row;
            if (!int.TryParse(m.Groups["row"].Value, out row) || row < 1 || row > 20)
                yield break;
            yield return null;
            row--;
            int col = "abcdefghijklmnopqrst".IndexOf(m.Groups["col"].Value);
            int currentPos = row * 20 + col;
            pixels[currentPos].OnInteract();
            yield return null;
            pixels[currentPos].OnInteractEnded();
            yield return new WaitForSeconds(0.02f);
            if (m.Groups["dirs"].Success)
            {
                var dirString = m.Groups["dirs"].Value;
                for (int i = 0; i < dirString.Length; i++)
                {
                    int ix = "urdl ".IndexOf(dirString[i]);
                    if (ix == 0) row--;
                    if (ix == 1) col++;
                    if (ix == 2) row++;
                    if (ix == 3) col--;
                    if (col < 0 || row < 0 || col > 19 || row > 19) yield break;
                    currentPos = row * 20 + col;
                    pixels[currentPos].OnInteract();
                    yield return null;
                    pixels[currentPos].OnInteractEnded();
                    yield return new WaitForSeconds(0.02f);
                }
            }
        }
        m = Regex.Match(command, @"^\s*clear\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame != 4)
            {
                yield return "sendtochaterror You are not in the \"Pixel painting\" minigame!";
                yield break;
            }
            yield return null;
            clearButton.OnInteract();
            yield break;
        }
        m = Regex.Match(command, @"^\s*sus(sy|picious)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame != 4)
            {
                yield return "sendtochaterror You are not in the \"Pixel painting\" minigame!";
                yield break;
            }
            yield return null;
            clearButton.OnInteract();
            yield return new WaitForSeconds(0.02f);
            var str = "............................0000000............022222220..........02222222220.........01220000000.........0120344...40......0001203444..40.....022012034444440.....012012033333330.....01101220000000......01101222222220......01101122222220......01101122222220......01101112222210......01101111111110.......0001110000110.........01110.01110.........01110.01110.........01110.01110..........000...000....";
            for (int c = '0'; c <= '4'; c++)
            {
                var color = new[] { 17, 1, 0, 10, 9 }[c - '0'];
                paintingColorButtons[color].OnInteract();
                yield return new WaitForSeconds(0.02f);
                var pix = Enumerable.Range(0, 400).Where(i => str[i] == c).ToArray();
                for (int i = 0; i < pix.Length; i++)
                {
                    pixels[pix[i]].OnInteract();
                    yield return null;
                    pixels[pix[i]].OnInteractEnded();
                    yield return new WaitForSeconds(0.02f);
                }
            }
            audio.PlaySoundAtTransform("breadinfrench", transform);
            yield return "sendtochat STOP STOP STOP FUCK FUCK FUCK GET OUT AAAAAAAAAAAAAAAAAAAAAAAAAA";
            yield break;
        }

        // Library
        m = Regex.Match(command, @"^\s*library\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            if (inGame)
            {
                if (currentGame == 5)
                    yield break;
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            if (mainButtons[0].GetComponentInChildren<TextMesh>().text == "Solve the module")
            {
                controlButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            mainButtons[2].OnInteract();
        }
        m = Regex.Match(command, @"^\s*(select|exit)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame != 5)
            {
                yield return "sendtochaterror You are not in the \"Library\" minigame!";
                yield break;
            }
            yield return null;
            bookButton.OnInteract();
            yield break;
        }
        if (command.StartsWith("select "))
        {
            if (currentGame != 5)
            {
                yield return "sendtochaterror You are not in the \"Library\" minigame!";
                yield break;
            }
            var book = command.Substring(7);
            var bookTitles = new string[] { "1984", "a christmas carol", "call of cthulhu", "diary of a wimpy kid", "fahrenheit 451", "moby dick", "the bells", "the cask of amontillado", "the ones who walk away from omelas", "the raven" };
            int ix = Array.IndexOf(bookTitles, book);
            if (ix == -1)
                yield break;
            yield return null;
            if (bookSelected)
            {
                bookButton.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            while (selectedBook != ix)
            {
                bookCycleButtons[0].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            bookButton.OnInteract();
            yield break;
        }
        m = Regex.Match(command, @"^\s*(?<page>prev(ious)?|next)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            if (currentGame != 5)
            {
                yield return "sendtochaterror You are not in the \"Library\" minigame!";
                yield break;
            }
            yield return null;
            if (m.Groups["page"].Value[0] == 'p')
                bookCycleButtons[1].OnInteract();
            else
                bookCycleButtons[0].OnInteract();
            yield break;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        if (inGame)
        {
            controlButton.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        if (mainButtons[0].GetComponentInChildren<TextMesh>().text != "Solve the module")
        {
            controlButton.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        while (((int)bomb.GetTime()) % 10 != solution)
            yield return true;
        mainButtons[0].OnInteract();
    }

    private string GetModuleCode()
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;
        foreach (Transform children in transform.parent)
        {
            var distance = (transform.position - children.position).magnitude;
            if (children.gameObject.name == "TwitchModule(Clone)" && (closest == null || distance < closestDistance))
            {
                closest = children;
                closestDistance = distance;
            }
        }
        return closest != null ? closest.Find("MultiDeckerUI").Find("IDText").GetComponent<UnityEngine.UI.Text>().text : null;
    }
}
