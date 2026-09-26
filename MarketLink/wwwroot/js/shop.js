var reservePrice = 0;
var reserveMax = 1;
var confirmOpen = "00:00";
var confirmClose = "23:59";
var confirmNoDay = "";

function formatMoney(amount) {
    return "$" + amount.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

function toMinutes(text) {
    var parts = text.split(":");
    return parseInt(parts[0]) * 60 + parseInt(parts[1]);
}

function toTimeText(minutes) {
    var hours = Math.floor(minutes / 60);
    var mins = minutes % 60;
    return (hours < 10 ? "0" : "") + hours + ":" + (mins < 10 ? "0" : "") + mins;
}

function todayText() {
    var now = new Date();
    var month = now.getMonth() + 1;
    var day = now.getDate();
    return now.getFullYear() + "-" + (month < 10 ? "0" : "") + month + "-" + (day < 10 ? "0" : "") + day;
}

function openReserve(button) {
    reservePrice = parseFloat(button.getAttribute("data-price"));
    reserveMax = parseInt(button.getAttribute("data-max"));

    document.getElementById("reserveStockPriceId").value = button.getAttribute("data-id");
    document.getElementById("reserveName").innerText = button.getAttribute("data-name");
    document.getElementById("reserveStall").innerText = button.getAttribute("data-stall");
    document.getElementById("reserveUnit").innerText = button.getAttribute("data-unit");
    document.getElementById("reservePrice").innerText = formatMoney(reservePrice);
    document.getElementById("reserveLeft").innerText = "(" + reserveMax + " " + button.getAttribute("data-unit") + " left)";
    document.getElementById("reserveImage").src = button.getAttribute("data-image");

    var reserveForm = document.getElementById("reserveForm");
    reserveForm.setAttribute("data-name", button.getAttribute("data-name"));
    reserveForm.setAttribute("data-unit", button.getAttribute("data-unit"));
    reserveForm.setAttribute("data-price", button.getAttribute("data-price"));
    reserveForm.setAttribute("data-stall", button.getAttribute("data-stall"));
    reserveForm.setAttribute("data-dates", button.getAttribute("data-dates"));
    reserveForm.setAttribute("data-open", button.getAttribute("data-open"));
    reserveForm.setAttribute("data-close", button.getAttribute("data-close"));
    reserveForm.setAttribute("data-no-day", "This stall has no selling day in the next 7 days.");

    var quantity = 1;
    var pageQuantity = document.getElementById("quantity");
    if (pageQuantity) {
        quantity = parseInt(pageQuantity.value) || 1;
    }
    if (quantity > reserveMax) quantity = reserveMax;
    var quantityInput = document.getElementById("reserveQuantity");
    quantityInput.value = quantity;
    quantityInput.max = reserveMax;

    var hasDay = button.getAttribute("data-dates") != "";
    document.getElementById("reserveNoDay").style.display = hasDay ? "none" : "";
    document.getElementById("reserveNowButton").disabled = !hasDay;

    updateReserveTotal();
    document.getElementById("reserveModal").classList.add("show");
}

function closeReserve() {
    document.getElementById("reserveModal").classList.remove("show");
}

function fillDateSelect(dateSelect, datesText) {
    dateSelect.innerHTML = "";
    if (!datesText) return;

    var dateList = datesText.split(",");
    for (var i = 0; i < dateList.length; i++) {
        var parts = dateList[i].split("-");
        var d = new Date(parseInt(parts[0]), parseInt(parts[1]) - 1, parseInt(parts[2]));
        var label = d.toLocaleDateString("en-GB", { weekday: "long", day: "2-digit", month: "2-digit" });
        if (dateList[i] == todayText()) {
            label = "Today, " + label;
        }
        var option = document.createElement("option");
        option.value = dateList[i];
        option.text = label;
        dateSelect.appendChild(option);
    }
}

function setupConfirmPickup(form) {
    confirmOpen = form.getAttribute("data-open") || "00:00";
    confirmClose = form.getAttribute("data-close") || "23:59";
    confirmNoDay = form.getAttribute("data-no-day") || "No pickup day available in the next 7 days.";
    fillDateSelect(document.getElementById("confirmDate"), form.getAttribute("data-dates"));
    buildConfirmSlots();
}

function buildConfirmSlots() {
    buildSlotsFor(document.getElementById("confirmDate"), document.getElementById("confirmSlots"),
        document.getElementById("confirmYesButton"), confirmOpen, confirmClose, confirmNoDay);
}

function buildSlotsFor(dateSelect, slotBox, reserveButton, openText, closeText, noDayText) {
    slotBox.innerHTML = "";

    if (dateSelect.value == "") {
        var noDay = document.createElement("span");
        noDay.className = "stock-low";
        noDay.innerText = noDayText;
        slotBox.appendChild(noDay);
        if (reserveButton) reserveButton.disabled = true;
        return;
    }

    var now = new Date();
    var nowMinutes = now.getHours() * 60 + now.getMinutes();
    var isToday = dateSelect.value == todayText();
    var start = toMinutes(openText);
    var close = toMinutes(closeText);
    var availableCount = 0;

    while (start < close) {
        var end = start + 60;
        if (end > close) end = close;

        var passed = isToday && end <= nowMinutes;
        var value = toTimeText(start) + "-" + toTimeText(end);
        var label = document.createElement("label");
        label.className = "slot";
        label.title = passed ? "This time has passed" : "";
        label.innerHTML = "<input type='radio' name='PickupSlot' value='" + value + "'"
            + (passed ? " disabled" : "")
            + (!passed && availableCount == 0 ? " checked" : "")
            + " /><span>" + toTimeText(start) + " – " + toTimeText(end) + "</span>";
        slotBox.appendChild(label);

        if (!passed) availableCount++;
        start = end;
    }

    if (availableCount == 0) {
        var note = document.createElement("span");
        note.className = "stock-low";
        note.style.width = "100%";
        note.innerText = "No pickup time left on this day. Please choose another day.";
        slotBox.appendChild(note);
        if (reserveButton) reserveButton.disabled = true;
    } else {
        if (reserveButton) reserveButton.disabled = false;
    }
}

function changeReserveQuantity(step) {
    var input = document.getElementById("reserveQuantity");
    var value = parseInt(input.value) || 1;
    value = value + step;
    if (value < 1) value = 1;
    if (value > reserveMax) value = reserveMax;
    input.value = value;
    updateReserveTotal();
}

function updateReserveTotal() {
    var input = document.getElementById("reserveQuantity");
    var value = parseInt(input.value) || 1;
    document.getElementById("reserveTotal").innerText = formatMoney(reservePrice * value);
    document.getElementById("reserveNowButton").innerText = "RESERVE NOW – " + formatMoney(reservePrice * value);
}

var pendingForm = null;
var pendingSubmitter = null;

function escapeHtml(text) {
    var div = document.createElement("div");
    div.innerText = text;
    return div.innerHTML;
}

function confirmRow(key, value, extraClass) {
    return "<div class='confirm-row " + (extraClass || "") + "'><span class='k'>" + key + "</span><span class='v'>" + escapeHtml(value) + "</span></div>";
}

function buildReserveSummary(form) {
    var price = parseFloat(form.getAttribute("data-price"));
    var quantity = parseInt(form.querySelector("[name=Quantity]").value) || 1;
    var unit = form.getAttribute("data-unit");

    var html = "";
    html += confirmRow("Product", form.getAttribute("data-name"));
    html += confirmRow("Quantity", quantity + " " + unit);
    html += confirmRow("Pick up at", form.getAttribute("data-stall"));
    html += confirmRow("Total", formatMoney(price * quantity), "total");
    return html;
}

function buildSkippedWarning(form) {
    var skipped = form.querySelectorAll(".skip-item");
    if (skipped.length == 0) return "";

    var html = "<div class='alert-ml warn-box confirm-warn'>";
    html += "<strong>" + (skipped.length == 1 ? "This item" : "These " + skipped.length + " items") + " will NOT be ordered:</strong>";
    html += "<ul>";
    for (var i = 0; i < skipped.length; i++) {
        html += "<li><span>" + escapeHtml(skipped[i].getAttribute("data-name")) + " · " + escapeHtml(skipped[i].getAttribute("data-qty")) + "</span>"
            + "<span class='reason'>" + escapeHtml(skipped[i].getAttribute("data-reason")) + "</span></li>";
    }
    html += "</ul>";
    html += "<span>They stay in your basket. Do you want to order the other items?</span>";
    html += "</div>";
    return html;
}

function buildCartSummary(form) {
    var html = buildSkippedWarning(form);
    html += confirmRow("Market", form.getAttribute("data-market"));
    html += confirmRow("Orders", form.getAttribute("data-orders") + " (one per farmer)");
    html += confirmRow("Receiver", form.querySelector("[name=PickupName]").value);
    html += confirmRow("Phone", form.querySelector("[name=PickupPhone]").value);
    html += confirmRow("Total", form.getAttribute("data-total"), "total");
    return html;
}

function closeConfirm() {
    document.getElementById("confirmModal").classList.remove("show");
    pendingForm = null;
    pendingSubmitter = null;
}

function setHiddenValue(form, name, value) {
    var input = form.querySelector("input[type=hidden][name=" + name + "]");
    if (!input) {
        input = document.createElement("input");
        input.type = "hidden";
        input.name = name;
        form.appendChild(input);
    }
    input.value = value;
}

function acceptConfirm() {
    if (pendingForm == null) return;
    var form = pendingForm;
    var submitter = pendingSubmitter;

    var dateSelect = document.getElementById("confirmDate");
    var slot = document.querySelector("#confirmSlots [name=PickupSlot]:checked");
    if (dateSelect.value == "" || !slot) {
        return;
    }
    setHiddenValue(form, "PickupDate", dateSelect.value);
    setHiddenValue(form, "PickupSlot", slot.value);
    form.setAttribute("data-confirmed", "1");
    document.getElementById("confirmYesButton").disabled = true;
    if (submitter && form.requestSubmit) {
        form.requestSubmit(submitter);
    } else {
        form.submit();
    }
}

document.addEventListener("submit", function (e) {
    var form = e.target;
    var type = form.getAttribute("data-confirm");
    if (!type) return;

    if (e.submitter && e.submitter.hasAttribute("formaction")) return;
    if (form.getAttribute("data-confirmed") == "1") return;

    e.preventDefault();
    pendingForm = form;
    pendingSubmitter = e.submitter || null;

    var body = document.getElementById("confirmBody");
    var yesButton = document.getElementById("confirmYesButton");
    if (type == "cart") {
        body.innerHTML = buildCartSummary(form);
        yesButton.innerText = form.querySelector(".skip-item") ? "Yes, order the rest" : "Yes, place order";
    } else {
        body.innerHTML = buildReserveSummary(form);
        yesButton.innerText = "Yes, place order";
    }
    yesButton.disabled = false;
    setupConfirmPickup(form);
    document.getElementById("confirmModal").classList.add("show");
    document.getElementById("confirmYesButton").focus();
});

document.addEventListener("keydown", function (e) {
    if (e.key != "Escape") return;
    var confirmModal = document.getElementById("confirmModal");
    var modal = document.getElementById("reserveModal");
    if (confirmModal && confirmModal.classList.contains("show")) {
        closeConfirm();
    } else if (modal && modal.classList.contains("show")) {
        closeReserve();
    }
});

function hideToast() {
    var toast = document.getElementById("toastMessage");
    if (toast) {
        toast.classList.add("hide");
        setTimeout(function () { toast.remove(); }, 300);
    }
}

if (document.getElementById("toastMessage")) {
    setTimeout(hideToast, 3500);
}

document.addEventListener("submit", function (e) {
    var action = e.target.getAttribute("action") || "";
    if (e.submitter && e.submitter.getAttribute("formaction")) {
        action = e.submitter.getAttribute("formaction");
    }
    action = action.toLowerCase();
    if (action.indexOf("/cart/add") >= 0 || action.indexOf("/favorites/add") >= 0 || action.indexOf("/favorites/remove") >= 0) {
        try { sessionStorage.setItem("ml_scroll", window.location.pathname + window.location.search + "|" + window.scrollY); } catch (err) { }
    }
});

(function () {
    var saved = null;
    try { saved = sessionStorage.getItem("ml_scroll"); sessionStorage.removeItem("ml_scroll"); } catch (err) { }
    if (saved) {
        var parts = saved.split("|");
        if (parts[0] == window.location.pathname + window.location.search) {
            window.scrollTo(0, parseInt(parts[1]));
        }
    }
})();

document.addEventListener("submit", function (e) {
    var form = e.target;
    if (e.defaultPrevented) return;
    if ((form.getAttribute("method") || "get").toLowerCase() != "post") return;

    if (form.getAttribute("data-submitting") == "1") {
        e.preventDefault();
        return;
    }
    form.setAttribute("data-submitting", "1");

    setTimeout(function () {
        var buttons = form.querySelectorAll("button[type=submit]");
        for (var i = 0; i < buttons.length; i++) {
            if (!buttons[i].disabled) {
                buttons[i].disabled = true;
                buttons[i].setAttribute("data-locked", "1");
            }
        }
    }, 0);
});

window.addEventListener("pageshow", function (e) {
    if (!e.persisted) return;
    var forms = document.querySelectorAll("form[data-submitting]");
    for (var i = 0; i < forms.length; i++) {
        forms[i].removeAttribute("data-submitting");
        forms[i].removeAttribute("data-confirmed");
    }
    var buttons = document.querySelectorAll("button[data-locked]");
    for (var j = 0; j < buttons.length; j++) {
        buttons[j].disabled = false;
        buttons[j].removeAttribute("data-locked");
    }
});

function scrollCarousel(button, direction) {
    var track = button.parentElement.querySelector(".carousel-track");
    var gap = 12;
    var step = track.children[0].offsetWidth + gap;
    var cardsPerView = Math.max(1, Math.floor((track.clientWidth + gap) / step));
    track.scrollBy({ left: direction * step * cardsPerView, behavior: "smooth" });
}

function updateCarouselButtons(track) {
    var box = track.parentElement;
    box.querySelector(".carousel-btn.prev").disabled = track.scrollLeft <= 10;
    box.querySelector(".carousel-btn.next").disabled = track.scrollLeft + track.clientWidth >= track.scrollWidth - 10;
}

(function () {
    var tracks = document.querySelectorAll(".carousel-track");
    for (var i = 0; i < tracks.length; i++) {
        var track = tracks[i];
        track.addEventListener("scroll", function () { updateCarouselButtons(this); });
        updateCarouselButtons(track);
    }
    window.addEventListener("resize", function () {
        for (var j = 0; j < tracks.length; j++) {
            updateCarouselButtons(tracks[j]);
        }
    });
})();

function setButtonLoading(button, text) {
    if (!button) return;
    if (!button.hasAttribute("data-label")) {
        button.setAttribute("data-label", button.innerHTML);
    }
    button.disabled = true;
    button.classList.add("loading");
    button.setAttribute("aria-busy", "true");
    button.innerHTML = "<span class='spinner' aria-hidden='true'></span><span></span>";
    button.lastChild.innerText = text;
}

function stopButtonLoading(button) {
    if (!button || !button.hasAttribute("data-label")) return;
    button.innerHTML = button.getAttribute("data-label");
    button.removeAttribute("data-label");
    button.disabled = false;
    button.classList.remove("loading");
    button.removeAttribute("aria-busy");
}

window.addEventListener("pageshow", function (e) {
    if (!e.persisted) return;
    var buttons = document.querySelectorAll("button.loading");
    for (var i = 0; i < buttons.length; i++) {
        stopButtonLoading(buttons[i]);
    }
});

document.addEventListener("click", function (e) {
    var pickers = document.querySelectorAll("details.day-picker[open]");
    for (var i = 0; i < pickers.length; i++) {
        if (!pickers[i].contains(e.target)) {
            pickers[i].removeAttribute("open");
        }
    }
});

document.addEventListener("keydown", function (e) {
    if (e.key != "Escape") return;
    var pickers = document.querySelectorAll("details.day-picker[open]");
    for (var i = 0; i < pickers.length; i++) {
        pickers[i].removeAttribute("open");
        pickers[i].querySelector("summary").focus();
    }
});
