var reservePrice = 0;
var reserveMax = 1;
var reserveOpen = "00:00";
var reserveClose = "23:59";

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
    reserveOpen = button.getAttribute("data-open");
    reserveClose = button.getAttribute("data-close");

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

    var quantity = 1;
    var pageQuantity = document.getElementById("quantity");
    if (pageQuantity) {
        quantity = parseInt(pageQuantity.value) || 1;
    }
    if (quantity > reserveMax) quantity = reserveMax;
    var quantityInput = document.getElementById("reserveQuantity");
    quantityInput.value = quantity;
    quantityInput.max = reserveMax;

    var dateSelect = document.getElementById("reserveDate");
    dateSelect.innerHTML = "";
    var dates = button.getAttribute("data-dates");
    if (dates != "") {
        var dateList = dates.split(",");
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

    buildSlots();
    updateReserveTotal();
    document.getElementById("reserveModal").classList.add("show");
}

function closeReserve() {
    document.getElementById("reserveModal").classList.remove("show");
}

function buildSlots() {
    buildSlotsFor(document.getElementById("reserveDate"), document.getElementById("reserveSlots"),
        document.getElementById("reserveNowButton"), reserveOpen, reserveClose);
}

function buildSlotsFor(dateSelect, slotBox, reserveButton, openText, closeText) {
    slotBox.innerHTML = "";

    if (dateSelect.value == "") {
        slotBox.innerHTML = "<span class='stock-low'>This stall has no selling day in the next 7 days.</span>";
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
    var dateSelect = form.querySelector("[name=PickupDate]");
    var slot = form.querySelector("[name=PickupSlot]:checked");

    var html = "";
    html += confirmRow("Product", form.getAttribute("data-name"));
    html += confirmRow("Quantity", quantity + " " + unit);
    html += confirmRow("Pick up at", form.getAttribute("data-stall"));
    html += confirmRow("Pickup day", dateSelect.options[dateSelect.selectedIndex].text);
    html += confirmRow("Pickup time", slot ? slot.value.replace("-", " – ") : "-");
    html += confirmRow("Total", formatMoney(price * quantity), "total");
    return html;
}

function buildCartSummary(form) {
    var html = "";
    html += confirmRow("Market", form.getAttribute("data-market"));
    html += confirmRow("Orders", form.getAttribute("data-orders") + " (one per farmer)");
    html += confirmRow("Receiver", form.querySelector("[name=PickupName]").value);
    html += confirmRow("Phone", form.querySelector("[name=PickupPhone]").value);
    html += confirmRow("Pickup", "Next selling day of each stall");
    html += confirmRow("Total", form.getAttribute("data-total"), "total");
    return html;
}

function closeConfirm() {
    document.getElementById("confirmModal").classList.remove("show");
    pendingForm = null;
    pendingSubmitter = null;
}

function acceptConfirm() {
    if (pendingForm == null) return;
    var form = pendingForm;
    var submitter = pendingSubmitter;
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
    if (type == "cart") {
        body.innerHTML = buildCartSummary(form);
    } else {
        body.innerHTML = buildReserveSummary(form);
    }
    document.getElementById("confirmYesButton").disabled = false;
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
