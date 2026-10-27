(function () {
    "use strict";

    /* ---------- Thema ---------- */

    var hoofd = document.documentElement;

    function actualiseerThemaKnop() {
        var knop = document.querySelector("[data-themaknop]");
        if (!knop) return;
        var donker = hoofd.dataset.thema !== "light";
        knop.setAttribute("aria-label", donker ? "Wissel naar het lichte thema" : "Wissel naar het donkere thema");
        knop.setAttribute("title", donker ? "Licht thema" : "Donker thema");
    }

    document.addEventListener("click", function (gebeurtenis) {
        var knop = gebeurtenis.target.closest("[data-themaknop]");
        if (!knop) return;
        var volgendThema = hoofd.dataset.thema === "light" ? "dark" : "light";
        hoofd.dataset.thema = volgendThema;
        try { localStorage.setItem("thema", volgendThema); } catch (e) { }
        actualiseerThemaKnop();
    });

    actualiseerThemaKnop();

    /* ---------- Mobiel menu ---------- */

    var balk = document.querySelector("[data-balk]");
    var menuKnop = document.querySelector("[data-menuknop]");

    if (balk && menuKnop) {
        menuKnop.addEventListener("click", function () {
            var open = balk.classList.toggle("is-open");
            menuKnop.setAttribute("aria-expanded", open ? "true" : "false");
            menuKnop.setAttribute("aria-label", open ? "Menu sluiten" : "Menu openen");
        });

        document.addEventListener("keydown", function (gebeurtenis) {
            if (gebeurtenis.key === "Escape" && balk.classList.contains("is-open")) {
                balk.classList.remove("is-open");
                menuKnop.setAttribute("aria-expanded", "false");
            }
        });
    }

    /* ---------- Vergrootglas (lightbox) ---------- */

    var dialoog = document.querySelector("[data-vergrootglas]");
    if (!dialoog) return;

    var dialoogImg = dialoog.querySelector("[data-vergrootglas-img]");
    var dialoogTekst = dialoog.querySelector("[data-vergrootglas-tekst]");
    var sluitKnop = dialoog.querySelector("[data-vergrootglas-sluit]");

    document.addEventListener("click", function (gebeurtenis) {
        var knop = gebeurtenis.target.closest("[data-vergroten]");
        if (!knop) return;
        dialoogImg.src = knop.getAttribute("data-bron") || "";
        dialoogImg.alt = knop.getAttribute("data-omschrijving") || "";
        dialoogTekst.textContent = knop.getAttribute("data-bijschrift") || "";
        if (typeof dialoog.showModal === "function") {
            dialoog.showModal();
        }
    });

    if (sluitKnop) {
        sluitKnop.addEventListener("click", function () {
            dialoog.close();
        });
    }

    dialoog.addEventListener("click", function (gebeurtenis) {
        var vlak = dialoog.getBoundingClientRect();
        if (gebeurtenis.clientX < vlak.left ||
            gebeurtenis.clientX > vlak.right ||
            gebeurtenis.clientY < vlak.top ||
            gebeurtenis.clientY > vlak.bottom) {
            dialoog.close();
        }
    });

    dialoog.addEventListener("close", function () {
        dialoogImg.removeAttribute("src");
    });

    /* ---------- Aftelklok (stage) — ververst automatisch bij datumwissel ---------- */

    var aftelklok = document.querySelector("[data-aftelklok]");

    function bijwerkAftelklok() {
        if (!aftelklok) return;
        var startdatum = aftelklok.getAttribute("data-startdatum");
        if (!startdatum) return;
        var start = new Date(startdatum + "T00:00:00");
        var vandaag = new Date();
        vandaag.setHours(0, 0, 0, 0);
        var dagen = Math.round((start - vandaag) / 86400000);
        var getalElement = aftelklok.querySelector("[data-aftelklok-getal]");
        var tekstElement = aftelklok.querySelector("[data-aftelklok-tekst]");
        if (dagen > 0) {
            if (getalElement) getalElement.textContent = String(dagen);
            if (tekstElement) tekstElement.firstChild.nodeValue = (dagen === 1 ? "dag" : "dagen") + " tot de start van mijn stage";
        } else {
            // Stage gestart of gestart geweest — klok verbergen.
            aftelklok.style.display = "none";
        }
    }

    if (aftelklok) {
        bijwerkAftelklok();
        setInterval(bijwerkAftelklok, 60000); // dagovergang automatisch vangen
    }

    /* ---------- Uploadvoorbeeldweergave ---------- */

    function naamAlsTekst(bestandsnaam) {
        if (!bestandsnaam || bestandsnaam.length <= 28) return bestandsnaam || "bestand";
        return bestandsnaam.slice(0, 12) + "…" + bestandsnaam.slice(-12);
    }

    document.querySelectorAll("input[type=file][data-voorbeeld]").forEach(function (invoer) {
        var doeldoek = document.getElementById(invoer.getAttribute("data-voorbeeld"));
        if (!doeldoek) return;
        var isMeervoudig = invoer.hasAttribute("multiple");
        invoer.addEventListener("change", function () {
            if (!invoer.files || invoer.files.length === 0) {
                doeldoek.innerHTML = "";
                return;
            }
            doeldoek.innerHTML = "";
            Array.prototype.forEach.call(invoer.files, function (bestand) {
                if (!bestand.type || bestand.type.indexOf("image/") !== 0) return;
                var afbeelding = document.createElement("img");
                afbeelding.alt = naamAlsTekst(bestand.name);
                afbeelding.title = "Voorbeeld: " + (bestand.name || "bestand");
                afbeelding.src = URL.createObjectURL(bestand);
                doeldoek.appendChild(afbeelding);
            });
            var noot = document.createElement("span");
            noot.className = "tekst-sm tekst-sub";
            if (isMeervoudig) {
                var aantal = invoer.files.length;
                noot.textContent = aantal + (aantal === 1 ? " afbeelding gekozen" : " afbeeldingen gekozen");
            } else {
                noot.textContent = "Bestand gekozen: " + naamAlsTekst(invoer.files[0].name);
            }
            doeldoek.appendChild(noot);
        });
    });
})();

(function () {
    "use strict";
    // Tekenteller voor het contactformulier (data-karakterteller="$id" op het teller-element).
    var tellers = document.querySelectorAll("textarea[data-karakterteller]");
    Array.prototype.forEach.call(tellers, function (veld) {
        var teller = document.getElementById("contactTeller");
        if (!teller) return;
        var max = parseInt(veld.getAttribute("maxlength"), 10) || 0;
        var bijwerken = function () {
            teller.textContent = veld.value.length + (max ? " / " + max : " tekens");
        };
        veld.addEventListener("input", bijwerken);
        bijwerken();
    });
})();
