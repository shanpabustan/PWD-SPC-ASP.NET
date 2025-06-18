document.addEventListener('DOMContentLoaded', function () {
    // Set the current date for Philippines (UTC+8)
    var dateInput = document.getElementById('dateApplied');
    var today = new Date();
    today.setHours(today.getHours() + 8); // Adjust to UTC+8 manually
    dateInput.value = today.toISOString().split('T')[0]; // ISO format YYYY-MM-DD


    // Add date format functionality for Date of Birth
    document.getElementById('DateOfBirth').addEventListener('input', function () {
        const inputDate = this.value;
        if (inputDate) {
            const formattedDate = new Date(inputDate);
            const month = formattedDate.getMonth() + 1; // Months are 0-indexed
            const day = formattedDate.getDate();
            const year = formattedDate.getFullYear();

            // Format the date as MM/DD/YYYY
            const formatted = `${month.toString().padStart(2, '0')}/${day.toString().padStart(2, '0')}/${year}`;

            // Display the formatted date below the input (if necessary)
            const formattedDateElement = document.getElementById('formattedDate');
            if (formattedDateElement) {
                formattedDateElement.textContent = `Formatted Date: ${formatted}`;
            }
        }
    });


    // Add event listeners for applicant type radio buttons
    const applicantTypeRadios = document.querySelectorAll('input[name="ApplicantType"]');
    applicantTypeRadios.forEach(radio => {
        radio.addEventListener('change', toggleDisabilityNumberField);
    });

    // Initial call to set the correct state on page load
    toggleDisabilityNumberField();

    // Add event listeners for employment status radio buttons
    const employmentStatusRadios = document.querySelectorAll('input[name="status-of-employment"]');
    employmentStatusRadios.forEach(radio => {
        radio.addEventListener('change', toggleEmploymentFields);
    });

    // Initial call to set the correct state on page load
    toggleEmploymentFields();

    // Add event listeners for occupation radio buttons
    const occupationRadios = document.querySelectorAll('input[name="Occupation"]');
    occupationRadios.forEach(radio => {
        radio.addEventListener('change', toggleOccupationOtherField);
    });

    // Initial call to set the correct state on page load
    toggleOccupationOtherField();
});



function toggleOccupationOtherField() {
    const otherRadio = document.getElementById('Occupation-Other');
    const otherTextField = document.getElementById('Occupation-Other-Text');
    const occupationHiddenInput = document.getElementById('occupationInput');

    if (!otherRadio || !otherTextField || !occupationHiddenInput) {
        console.error("Occupation 'Other' elements not found!");
        return;
    }

    const isOtherSelected = otherRadio.checked;
    otherTextField.disabled = !isOtherSelected; // Enable if 'Other' is selected

    if (isOtherSelected) {
        otherTextField.value = ''; // Clear previous input
        occupationHiddenInput.value = ''; // Clear hidden input
    } else {
        occupationHiddenInput.value = otherTextField.value || "Other"; // Use "Other" if input is empty
    }

    // Update the hidden input when the user types in the text field
    otherTextField.addEventListener('input', function () {
        occupationHiddenInput.value = otherTextField.value; // Update hidden input with text field value
    });

    console.log(`Other occupation selected: ${isOtherSelected}`);
}

// Function to toggle disability number field
function toggleDisabilityNumberField() {
    const disabilityNumberElement = document.querySelector('input[name="DisabilityNumber"]');
    const applicantTypeElement = document.querySelector('input[name="ApplicantType"]:checked');

    if (!applicantTypeElement) {
        disabilityNumberElement.disabled = true;
        disabilityNumberElement.value = ''; // Clear the field if it's disabled
        return; // Exit if no applicant type is selected
    }

    const applicantType = applicantTypeElement.value;

    if (applicantType === 'New Applicant') {
        disabilityNumberElement.disabled = true;
        disabilityNumberElement.value = '';  // Clear the field if it's disabled
        disabilityNumberElement.classList.remove('is-invalid'); // Clear validation errors
    } else if (applicantType === 'Renewal') {
        disabilityNumberElement.disabled = false;
    }
}

function toggleEmploymentFields() {
    const isSelfEmployed = document.getElementById('status-Self-employed').checked;
    const governmentRadio = document.getElementById('categ-Government');
    const privateRadio = document.getElementById('categ-Private');
    const isUnemployed = document.getElementById('status-Unemployed').checked;

    // If 'Self-Employed' is selected
    if (isSelfEmployed) {
        privateRadio.checked = true; // Automatically select 'Private'
        governmentRadio.disabled = true; // Disable 'Government'
        governmentRadio.checked = false; // Uncheck 'Government' if it's disabled 
    }

    // Disable other employment fields if 'Unemployed' is selected
    const employmentFields = document.querySelectorAll(
        'input[name="CategoryOfEmployment"], input[name="TypeOfEmployment"], input[name="Occupation"]'
    );

    employmentFields.forEach(field => {
        field.disabled = isUnemployed;
        if (isUnemployed) {
            field.checked = false; // Uncheck the fields if they are being disabled
        }
    });
}

let current_step = 0;
const stepCount = 6;
const step = document.getElementsByClassName('step');
const prevBtn = document.getElementById('prev-btn');
const nextBtn = document.getElementById('next-btn');
const submitBtn = document.getElementById('submit-btn');
const successDiv = document.getElementById('success');

step[current_step].classList.add('d-block');
updateButtonVisibility();

function updateButtonVisibility() {
    prevBtn.classList.toggle('d-none', current_step === 0);
    submitBtn.classList.toggle('d-none', current_step !== stepCount);
    nextBtn.classList.toggle('d-none', current_step === stepCount);
}

function progress(value) {
    document.getElementsByClassName('progress-bar')[0].style.width = `${value}%`;
}

function validateEmail(email) {
    const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return re.test(String(email).toLowerCase());
}

function validateStep() {
    let valid = true;
    let currentStepFields = step[current_step].querySelectorAll('input, select, textarea');
    const alertsShown = new Set(); // Track alerts to prevent multiple pop-ups

    currentStepFields.forEach(field => {
        // Check for required fields
        if (field.hasAttribute('required') && !field.value.trim()) {
            valid = false;
            field.classList.add('is-invalid');
        } else {
            field.classList.remove('is-invalid');
        }

        // Check for email validation
        if (field.type === 'email' && field.value.trim() && !validateEmail(field.value)) {
            valid = false;
            field.classList.add('is-invalid');
            Swal.fire({
                icon: 'error',
                title: 'Invalid Email',
                text: 'Please enter a valid email address.'
            });
        }

        // Check for unchecked radio buttons, except for step 5
        if (field.type === 'radio' && field.hasAttribute('required')) {
            const radioGroupName = field.name;

            // Skip validation for 'Occupation' group
            if (radioGroupName === "Occupation") {
                return;
            }

            const checked = Array.from(document.querySelectorAll(`input[name="${radioGroupName}"]`)).some(radio => radio.checked);
            if (!checked && !alertsShown.has(radioGroupName)) {
                valid = false;

                // Map for friendly names
                const errormsg = {
                    "CivilStatus": "Civil Status",
                    "EducationalAttainment": "Educational Attainment",
                    "StatusOfEmployment": "Status of Employment"
                };

                let message = "Please select " + (errormsg[radioGroupName] || radioGroupName);
                Swal.fire({
                    icon: 'error',
                    title: 'Validation Error',
                    text: message,
                });
                alertsShown.add(radioGroupName); // Mark alert as shown
            }
        }
    });

    // Specific validations for step 0 (Applicant Type)
    if (current_step === 0) {
        const applicantTypeElement = document.querySelector('input[name="ApplicantType"]:checked');
        const disabilityNumberElement = document.querySelector('input[name="DisabilityNumber"]');

        if (!applicantTypeElement) {
            Swal.fire({
                icon: 'error',
                title: 'Applicant Type Required',
                text: 'Please select an Applicant type.'
            });
            valid = false;
        } else if (applicantTypeElement.value === 'Renewal' && (!disabilityNumberElement || !disabilityNumberElement.value.trim())) {
            Swal.fire({
                icon: 'error',
                title: 'Disability Number Required',
                text: 'Persons with Disability Number is required for renewal applicants.'
            });
            valid = false;
            disabilityNumberElement.classList.add('is-invalid');
        }
    }

    // Specific validations for step 2 (Type of Disability and Cause of Disability)
    if (current_step === 2) {
        const typeOfDisabilityChecked = Array.from(document.querySelectorAll('input[name="TypeOfDisability"]')).some(radio => radio.checked);
        const causeOfDisabilityChecked = Array.from(document.querySelectorAll('input[name="CauseOfDisability"]')).some(radio => radio.checked);

        if (!typeOfDisabilityChecked) {
            Swal.fire({
                icon: 'error',
                title: 'Type of Disability Required',
                text: 'Please select a Type of Disability.'
            });
            valid = false;
        }

        if (!causeOfDisabilityChecked) {
            Swal.fire({
                icon: 'error',
                title: 'Cause of Disability Required',
                text: 'Please select a Cause of Disability.'
            });
            valid = false;
        }
    }

    // Specific validations for step 3 (Barangay)
    if (current_step === 3) {
        const dropdown = document.getElementById('brgy');
        const selectedValue = dropdown.value;

        if (selectedValue === "No Selected Value") {
            Swal.fire({
                icon: 'error',
                title: 'Barangay Selection Required',
                text: 'Please select a Barangay.'
            });
            valid = false;
        }
    }

    return valid;
}

nextBtn.addEventListener('click', () => {
    if (validateStep()) {
        current_step++;
        updateSteps();
    }
});

prevBtn.addEventListener('click', () => {
    if (current_step > 0) {
        current_step--;
        updateSteps();
    }
});

submitBtn.addEventListener('click', () => {
    if (validateStep()) {
        setTimeout(() => {
            step[stepCount].classList.remove('d-block');
            step[stepCount].classList.add('d-none');
            prevBtn.classList.add('d-none');
            submitBtn.classList.add('d-none');
            successDiv.classList.remove('d-none');
            successDiv.classList.add('d-block');
        }, 3000);
    }
});

function updateSteps() {
    Array.from(step).forEach((s, index) => {
        s.classList.toggle('d-block', index === current_step);
        s.classList.toggle('d-none', index !== current_step);
    });
    progress((current_step / stepCount) * 100);
    updateButtonVisibility();
}