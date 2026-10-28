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

    /* ---------- Stageklok — telt af, toont de voortgang of sluit af ---------- */

    var stageklokken = document.querySelectorAll("[data-stageklok]");

    function dagVan(waarde) {
        if (!waarde) return null;
        var delen = waarde.split("-");
        if (delen.length !== 3) return null;
        var datum = new Date(Number(delen[0]), Number(delen[1]) - 1, Number(delen[2]));
        datum.setHours(0, 0, 0, 0);
        return isNaN(datum.getTime()) ? null : datum;
    }

    function dagenTussen(vanaf, tot) {
        return Math.round((tot - vanaf) / 86400000);
    }

    function bijwerkStageklok(klok) {
        var start = dagVan(klok.getAttribute("data-start"));
        var eind = dagVan(klok.getAttribute("data-eind"));
        if (!start && !eind) return;

        var vandaag = new Date();
        vandaag.setHours(0, 0, 0, 0);
        if (start && eind && eind < start) eind = null;

        var fase, getal, eenheid, tekst, voortgang = 0;

        if (start && vandaag < start) {
            fase = "gepland";
            getal = dagenTussen(vandaag, start);
            eenheid = getal === 1 ? "dag" : "dagen";
            tekst = "tot de start van mijn stage";
        } else if (eind && vandaag > eind) {
            fase = "afgerond";
            getal = start ? dagenTussen(start, eind) + 1 : 0;
            eenheid = getal === 1 ? "dag" : "dagen";
            tekst = "stage afgerond";
            voortgang = 1;
        } else {
            fase = "bezig";
            getal = start ? dagenTussen(start, vandaag) + 1 : 0;
            var totaal = start && eind ? dagenTussen(start, eind) + 1 : 0;
            eenheid = totaal > 0 ? "van " + totaal : (getal === 1 ? "dag" : "dagen");
            tekst = totaal > 0 ? "stage dag" : "sinds de start van mijn stage";
            voortgang = totaal > 0 ? Math.max(0, Math.min(1, getal / totaal)) : 0;
        }

        var labels = { gepland: "Aftellen", bezig: "Stage loopt", afgerond: "Afgerond" };
        var labelElement = klok.querySelector("[data-stage-label]");
        var getalElement = klok.querySelector("[data-stage-getal]");
        var eenheidElement = klok.querySelector("[data-stage-eenheid]");
        var tekstElement = klok.querySelector("[data-stage-tekst]");
        var balk = klok.querySelector("[data-stage-balk]");
        var vulling = klok.querySelector("[data-stage-vulling]");
        var procent = Math.round(voortgang * 100);

        if (labelElement) labelElement.textContent = labels[fase];
        if (getalElement) getalElement.textContent = String(getal);
        if (eenheidElement) eenheidElement.textContent = eenheid;
        if (tekstElement) tekstElement.textContent = tekst;

        klok.classList.remove("stageklok--gepland", "stageklok--bezig", "stageklok--afgerond");
        klok.classList.add("stageklok--" + fase);

        if (balk) balk.hidden = fase === "gepland" || procent === 0;
        if (vulling) vulling.style.width = procent + "%";
        if (balk) balk.setAttribute("aria-label", procent + "% van de stageperiode voltooid");
    }

    if (stageklokken.length > 0) {
        var werkStageklokkenBij = function () {
            Array.prototype.forEach.call(stageklokken, bijwerkStageklok);
        };
        werkStageklokkenBij();
        setInterval(werkStageklokkenBij, 60000); // dagovergang automatisch vangen
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
