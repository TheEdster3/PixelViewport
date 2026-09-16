const config = {
  repoUrl: "https://github.com/OWNER/PixelViewport",
  designPartnerUrl: ""
};

for (const link of document.querySelectorAll("[data-repo-link]")) {
  link.href = config.repoUrl;
}

for (const button of document.querySelectorAll("[data-contact]")) {
  if (!config.designPartnerUrl) continue;

  button.disabled = false;
  button.textContent = "Request a technical review";
  button.addEventListener("click", () => window.location.assign(config.designPartnerUrl));
}
